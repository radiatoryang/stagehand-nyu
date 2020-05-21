using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

namespace Stagehand
{

    public class StagehandCSV : MonoBehaviour
    {
        public static StagehandCSV Instance;

        public static string lastReport { get; private set; }

		public string downloadFromURL = "url";
		string urlData;
		public string debugEditorPathRoot = "C:\\Users\\Robert\\Desktop\\gamecenterlive\\endOfYearShow\\";
		public string[] writeToFilepaths = new string[8];
		string[,] csvData = new string[,] { {"C0", "C1", "C2", "C3", "C4", "C5", "C6", "C7"} };
		int currentRowIndex=0;

		public string htmlFilepath = "..\\Data_HTML_Full.html";
		public string htmlTemplatePath = "..\\Data_HTML_Template.html";

		void Start () {
			Application.targetFrameRate = 30;

			if ( Application.isEditor ) {
				for ( int i=0; i<writeToFilepaths.Length; i++) {
					if ( writeToFilepaths[i].StartsWith("..") ) {
						writeToFilepaths[i] = debugEditorPathRoot + writeToFilepaths[i].Substring(3);
					}
				}
				htmlFilepath = debugEditorPathRoot + htmlFilepath.Substring(3);
				htmlTemplatePath = debugEditorPathRoot + htmlTemplatePath.Substring(3);
			} else {
				downloadFromURL = PlayerPrefs.GetString( "downloadFromURL", downloadFromURL);
				var newWriteToFilepaths = PlayerPrefs.GetString( "writeToFilepaths", ",,,,,,,").Split(new string[] {","}, System.StringSplitOptions.None );
				for ( int i=0; i<newWriteToFilepaths.Length; i++) {
					if ( !string.IsNullOrEmpty(newWriteToFilepaths[i]) ) {
						writeToFilepaths[i] = newWriteToFilepaths[i];
					}
				}
				htmlFilepath = PlayerPrefs.GetString("htmlFilepath", "..\\Data_HTML_Full.html");
				htmlTemplatePath = PlayerPrefs.GetString("htmlTemplatePath", "..\\Data_HTML_Template.html");
			}
			StartRefresh();
		}

		void OnDisable() {
			// move back any videos, cleanup!
			for(int column=0; column < lastVideoRows.Length; column++) {
				if ( writeToFilepaths[column].EndsWith(".mp4") == false || File.Exists( writeToFilepaths[column] ) == false) {
					continue;
				}
				if ( lastVideoRows[column] > 0 ) {
					var oldVideoPath = GetVideoPathFor( column, lastVideoRows[column] );
					File.Move( writeToFilepaths[column], oldVideoPath );
				}
			}

			if ( Application.isEditor ) { return; }

			PlayerPrefs.SetString( "downloadFromURL", downloadFromURL);
			PlayerPrefs.SetString( "writeToFilepaths", string.Join(",", writeToFilepaths) );
			PlayerPrefs.SetString( "htmlFilepath", htmlFilepath );
			PlayerPrefs.SetString( "htmlTemplatePath", htmlTemplatePath );
			PlayerPrefs.Save();
		}

		void Update () {
			// paste URL
			if ( Application.isEditor && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V) ) {
				downloadFromURL = GUIUtility.systemCopyBuffer;
			}
		}

		string lastDownloadStatus = "";
		void OnGUI () {
			var skin = GUI.skin;
			GUILayout.BeginArea( new Rect(0,0, Screen.width, Screen.height) );

			GUILayout.BeginHorizontal(skin.box);
			if ( GUILayout.Button("Refresh URL")) {
				StartRefresh();
			}
			GUI.enabled = csvData != null && csvData.GetLength(1) > 2 && !downloadingVideos;
			if ( GUILayout.Button(downloadingVideos ? "downloading videos, please wait..." : "Download all videos (will take a while!)") ) {
				StartVideoDownload();
			}
			GUI.enabled = true;
			skipIfFileExists = GUILayout.Toggle(skipIfFileExists, "skip download if file exists" );
			downloadFromURL = GUILayout.TextField(downloadFromURL);
			// GUILayout.Label(" last report: " + lastReport);

			GUILayout.EndHorizontal();


			GUILayout.BeginHorizontal(skin.box);
			if ( downloadingVideos ) {
				GUILayout.Label("DOWNLOADING VIDEOS, please wait!\n" + lastDownloadStatus);
				GUILayout.Label( (downloadProgress * 100f).ToString("F0") + "%" );
			} else {
				if ( GUILayout.Button("-", GUILayout.MaxWidth(32)) ) {
					int newCurrentRowIndex = Mathf.Max(0, currentRowIndex - 1);
					if ( currentRowIndex != newCurrentRowIndex ) {
						RefreshToFiles(currentRowIndex, newCurrentRowIndex);
					}
					currentRowIndex = newCurrentRowIndex;
				}
				GUILayout.Label( currentRowIndex.ToString(), GUILayout.MaxWidth(32) );
				int nextIndex = Mathf.Min(csvData.GetLength(1)-1, currentRowIndex + 1);
				if ( GUILayout.Button("+", GUILayout.MaxWidth(32)) ) {
					int newCurrentRowIndex = nextIndex;
					if ( currentRowIndex != newCurrentRowIndex ) {
						RefreshToFiles(currentRowIndex, newCurrentRowIndex);
					}
					currentRowIndex = newCurrentRowIndex;
				}

				if ( currentRowIndex > 0 && csvData.GetLength(1) > 2 && Time.timeSinceLevelLoad > 1f) {
					GUILayout.FlexibleSpace();
					GUILayout.Label("NOW: " + csvData[1, currentRowIndex]);
					GUILayout.FlexibleSpace();
					GUILayout.Label("NEXT: " + csvData[1, nextIndex]);
				}
			}
			GUILayout.EndHorizontal();

			if ( Time.timeSinceLevelLoad < errorTimestamp + 10f ) {
				var oldColor = GUI.color;
				GUI.color = new Color( Mathf.Abs(Mathf.Sin(Time.timeSinceLevelLoad * 10f)), 0f, 0f, 1f );
				GUILayout.BeginHorizontal(skin.box);
				GUILayout.Label( errorText );
				GUILayout.EndHorizontal();
				GUI.color = oldColor;
			}


			GUILayout.BeginVertical(skin.box);
			for( int i=0; i<writeToFilepaths.Length; i++) {
				GUILayout.BeginHorizontal(skin.box);
				GUILayout.Label( "Column " + i.ToString(), GUILayout.Width(80) );
				// if ( GUILayout.Button("Save As...", GUILayout.MaxWidth(80) )) {
				// 	writeToFilepaths[i] = StandaloneFileBrowser.SaveFilePanel("Save File", Application.dataPath, "Column" + i.ToString(), "txt");
				// }
				writeToFilepaths[i] = GUILayout.TextField(writeToFilepaths[i] );
				GUILayout.EndHorizontal();
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label( "All Data HTML", GUILayout.Width(90) );
			htmlFilepath = GUILayout.TextField( htmlFilepath );
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label( "HTML template", GUILayout.Width(90) );
			htmlTemplatePath = GUILayout.TextField( htmlTemplatePath );
			GUILayout.EndHorizontal();

			GUILayout.EndVertical();

			GUILayout.EndArea();
		}

		// downloads video files at all URLs in a column
		public void StartVideoDownload() {
			if (downloadingVideos) {
				return;
			}
			StartCoroutine( VideoDownloadCoroutine() );
		}

		string SanitizeName(string name) {
			return name.Replace(":", "_").Replace(" ", "_").Replace(".", "").Replace("\\", "").Replace("/", "").Replace("!", "");
		}

		string GetVideoPathFor(int columnIndex, int rowIndex ) {
			string folder = writeToFilepaths[columnIndex].Substring(0, writeToFilepaths[columnIndex].Length-4) + "\\";
			return folder + rowIndex.ToString("D2") + "_" + SanitizeName(csvData[1, rowIndex]) + ".mp4";
		}

		bool downloadingVideos = false, skipIfFileExists = true;
		float downloadProgress = 0f;
		IEnumerator VideoDownloadCoroutine() {
			downloadingVideos = true;

			// gather video URLs
			int rowCount = csvData.GetLength(1);
			var allVideoURLs = new Dictionary<string, string>(); // key = file path, value = url if any
			for (int columnIndex=0; columnIndex<writeToFilepaths.Length; columnIndex++) {
				if ( writeToFilepaths[columnIndex].EndsWith(".mp4") ) {
					for (int rowIndex=1; rowIndex<rowCount; rowIndex++) {
						if ( string.IsNullOrWhiteSpace( csvData[columnIndex, rowIndex] ) || csvData[columnIndex, rowIndex].Length < 7 || !csvData[columnIndex, rowIndex].StartsWith("http") ) { // not a valid URL, can't download
							continue;
						}
						if ( skipIfFileExists && File.Exists( GetVideoPathFor(columnIndex, rowIndex) ) ) {
							continue;
						}
						allVideoURLs.Add( GetVideoPathFor(columnIndex, rowIndex), csvData[columnIndex, rowIndex] );
					}
				}
			}
			Debug.LogFormat("VideoDownloadCoroutine found {0} videos to download!", allVideoURLs.Count );

			int downloadCount = 0;
			// now download each video URL
			foreach (var kvp in allVideoURLs ) {

				string url = ProcessGoogleDriveURL( kvp.Value );
				// if ( url == kvp.Value ) {
				// 	// Debug.Log(i.ToString() + ". " + csvData[1, i] + ": not a google drive video link?" );
				// 	continue; // bail out, the link is empty or it isn't a google drive link? idk
				// }

				string filepath = kvp.Key;
				lastDownloadStatus = "SAVING " + filepath + " from " + url;
				Debug.Log( lastDownloadStatus );

				var web = new UnityWebRequest(url);
				web.downloadHandler = new DownloadHandlerFile(filepath);
				yield return web.SendWebRequest();
				if ( web.isNetworkError || web.isHttpError ) {
					Debug.LogWarning( web.error );
				}
				while ( !web.downloadHandler.isDone ) {
					yield return 0;
				}

				// check to see if the download failed because filesize is over 100 MB? need to scan for the confirm code
				if ( web.downloadedBytes <= 16000) { // file size is too small because it's a webpage, so...
					Debug.Log("scanning for confirm code...");
					// get the text
					string textData = File.ReadAllText( filepath );
					var textSplit = textData.Split(new string[] { "confirm=" }, System.StringSplitOptions.None);
					var confirmCode = textSplit[1].Substring(0, 4);
					url += "&confirm=" + confirmCode;

					lastDownloadStatus = "SAVING " + filepath + " from " + url;
					Debug.Log( lastDownloadStatus );

					web.Dispose();

					web = new UnityWebRequest(url);
					web.downloadHandler = new DownloadHandlerFile(filepath);
					yield return web.SendWebRequest();
					if ( web.isNetworkError || web.isHttpError ) {
						Debug.LogWarning( web.error );
					}
					while ( !web.downloadHandler.isDone ) {
						yield return 0;
					}

					if ( web.downloadedBytes <= 16000) {
						Debug.Log("confirm code didn't work... shit");
					}
					
				} else {
					web.Dispose();
				}

				// var bytes = web.downloadHandler.data;
				
				// using (FileStream fls = new FileStream(filepath, FileMode.Create))
				// {
				// 	fls.Write(bytes, 0, bytes.Length);
				// }
				// Debug.Log("FINISHED " + filepath );

				downloadCount++;
				downloadProgress = 1f * downloadCount / allVideoURLs.Count;
			}


			downloadingVideos = false;
		}

        public void StartRefresh()
        {
            StartCoroutine(RefreshCoroutine(downloadFromURL));
        }

		float errorTimestamp = -999f;
		string errorText = "";
		int[] lastVideoRows = new int[8];
		void RefreshToFiles(int oldRow, int newRow) {
			int maxX = csvData.GetUpperBound(0);
			string line = "";
			for ( int x=0; x<maxX; x++) {
				line += x.ToString() + ": " + csvData[x, newRow] + " | ";
			}
			Debug.Log(line);

			for (int i=0; i<writeToFilepaths.Length; i++ ) {
				if ( string.IsNullOrEmpty(writeToFilepaths[i]) ) {
					continue;
				}
				if ( writeToFilepaths[i].EndsWith(".mp4") ) {
					
					if ( File.Exists( writeToFilepaths[i]) ) {
						var oldVideoPath = GetVideoPathFor( i, lastVideoRows[i] );
						try {
							File.Move( writeToFilepaths[i], oldVideoPath ); // cut and paste video back to its old name
						} catch {
							errorText = "ERROR! You have to hide the video source in OBS before I can swap the MP4 files.";
							errorTimestamp = Time.timeSinceLevelLoad;
						}
					}

					var newVideoPath = GetVideoPathFor( i, newRow );
					if ( File.Exists( newVideoPath ) ) {
						try {
							File.Move( newVideoPath, writeToFilepaths[i] ); // cut and paste next video to current video
							lastVideoRows[i] = newRow;
						} catch {	
							errorText = "ERROR! You have to hide the video source in OBS before I can swap the MP4 files.";
							errorTimestamp = Time.timeSinceLevelLoad;
						}
					}
					continue;
				}
				if ( SaveIfImage( csvData[i, newRow], writeToFilepaths[i] ) ) {
					continue;
				}
				StreamWriter writer = new StreamWriter(writeToFilepaths[i], false);
				string data = csvData[i, newRow];
				if ( data.StartsWith("http://") ) { 
					data = data.Substring(7); // ignore the http:// preamble!
				}
				if ( data.StartsWith("https://") ) { 
					data = data.Substring(8); // ignore the https:// preamble!
				}
				if ( data.Length <= 4 && (data.StartsWith("BFA") || data.StartsWith("MFA")) ) {
					data = data.Substring(0, 3);
				}
				writer.WriteLine(data);
				writer.Close();
			}


			// refresh to HTML
			string htmlTemplate = File.ReadAllText( htmlTemplatePath );
			var rowData = Enumerable.Range(0, csvData.GetLength(0))
                .Select(x => csvData[x, newRow])
                .ToArray();
			string htmlData = string.Format( htmlTemplate, rowData); // replace all tokens in the template with the actual CSV data
			StreamWriter htmlWriter = new StreamWriter(htmlFilepath, false);
			htmlWriter.WriteLine( htmlData );
			htmlWriter.Close();
		}

        /// <summary>
        /// The main editor coroutine that fetches URLS and saves the files into the project.
        /// </summary>
        IEnumerator RefreshCoroutine(string url)
        {
			// url = FixURL( url );
			var results = UnityWebRequest.Get( url );
			yield return results.SendWebRequest(); // SendWebRequest();

			// handle an unknown internet error
			if (results.isNetworkError)
			{
				// Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}> and error message was: {1}", results.url, results.error);
				Debug.LogFormat("\n- [ERROR] {0}: {1}", url, results.error);
				yield break;
			}

			// make sure the fetched file isn't just a Google login page
			if (results.downloadHandler.text.Contains("google-site-verification")) {
				// Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}> because the Google Doc didn't have public link sharing enabled", results.url);
				Debug.LogFormat("\n- [ERROR] {0}: This Google Docs share link does not have 'VIEW' access; make sure you enable link sharing.", url);
				yield break;
			}

			urlData = results.downloadHandler.text;
			lastReport = string.Format("\n- [SUCCESS] {0}", url);

			csvData = SplitCsvGrid( urlData );
			DebugOutputGrid(csvData);
	
			results.Dispose(); // I don't know if this is actually necessary but let's do it anyway
        }

		const string googleDrivePrefix = "https://drive.google.com/file/d/";
		const string googleDrivePrefix2 = "https://drive.google.com/open?id=";
		const string googleDriveDownload = "https://drive.google.com/uc?id={0}&export=download";

		string ProcessGoogleDriveURL(string url) {
			if ( url.StartsWith(googleDrivePrefix) ) {
				var id = url.Substring( googleDrivePrefix.Length, 33);
				return string.Format( googleDriveDownload, id );
			}
			if ( url.StartsWith(googleDrivePrefix2 ) ) {
				var id = url.Substring( googleDrivePrefix2.Length, 33);
				return string.Format( googleDriveDownload, id);
			}
			return url;
		}

		bool SaveIfImage(string url, string filepath)
		{
			string id = "";
			if ( url.StartsWith(googleDrivePrefix) ) {
				id = url.Substring( googleDrivePrefix.Length, 33);
				StartCoroutine( SaveImage( string.Format( googleDriveDownload, id), filepath ) );
				return true;
			}
			if ( url.StartsWith(googleDrivePrefix2 ) ) {
				id = url.Substring( googleDrivePrefix2.Length, 33);
				StartCoroutine( SaveImage( string.Format( googleDriveDownload, id), filepath ) );
				return true;
			}
			if ( url.EndsWith(".jpg") || url.EndsWith(".jpeg") || url.EndsWith(".png") ) {
				StartCoroutine( SaveImage( url, filepath ) );
				return true;
			}
			return false;
			// https://drive.google.com/file/d/1NZo8UVre9OIKBxDfOYh0Gv5h1-gdjWWJ/view/
			// https://drive.google.com/uc?id=1NZo8UVre9OIKBxDfOYh0Gv5h1-gdjWWJ&export=download
		}

		IEnumerator SaveImage(string url, string filepath) {
			var web = new UnityWebRequest(url);
			web.downloadHandler = new DownloadHandlerBuffer();
			yield return web.SendWebRequest();
			if ( web.isNetworkError || web.isHttpError ) {
				Debug.LogWarning( web.error );
			}

			while ( !web.downloadHandler.isDone ) {
				yield return 0;
			}
			var bytes = web.downloadHandler.data;

			// re-encode into JPG
			if ( url.EndsWith(".png") ) {
				var tex = new Texture2D(2,2);
				tex.LoadImage( bytes );
				bytes = tex.EncodeToJPG(80);
			}
			
			using (FileStream fls = new FileStream(filepath, FileMode.Create))
			{
				fls.Write(bytes, 0, bytes.Length);
			}

			web.Dispose();
		}

        public static string FixURL(string url)
        {
            // if it's a Google Docs URL, then grab the document ID and reformat the URL
            if (url.StartsWith("https://docs.google.com/document/d/"))
            {
                var docID = url.Substring( "https://docs.google.com/document/d/".Length, 44 );
                return string.Format("https://docs.google.com/document/export?format=txt&id={0}&includes_info_params=true", docID);
            }
            if (url.StartsWith("https://docs.google.com/spreadsheets/d/"))
            {
                var docID = url.Substring( "https://docs.google.com/spreadsheets/d/".Length, 44 );
                return string.Format("https://docs.google.com/spreadsheets/export?format=tsv&id={0}", docID);
				// // https://docs.google.com/spreadsheets/export?format=tsv&id=117QYyK-OLZ6vIpmEXI_dmksHyzJF6ygZGc-BsBoe_B0&gid=394660007
            }
            return url;
        }

        public static string UnfixURL(string url)
        {
           // if it's a Google Docs URL, then grab the document ID and reformat the URL
            if (url.StartsWith("https://docs.google.com/document/export?format=txt"))
            {
                var docID = url.Substring( "https://docs.google.com/document/export?format=txt&id=".Length, 44 );
                return string.Format("https://docs.google.com/document/d/{0}/edit", docID);
            }
            if (url.StartsWith("https://docs.google.com/spreadsheets/export?format=tsv"))
            {
                var docID = url.Substring( "https://docs.google.com/spreadsheets/export?format=tsv&id=".Length, 44 );
                return string.Format("https://docs.google.com/spreadsheets/d/{0}", docID);
            }
            return url;
        }

		// outputs the content of a 2D array, useful for checking the importer
		static public void DebugOutputGrid(string[,] grid)
		{
			string textOutput = ""; 
			for (int y = 0; y < grid.GetUpperBound(1); y++) {	
				for (int x = 0; x < grid.GetUpperBound(0); x++) {
	
					textOutput += grid[x,y]; 
					textOutput += "|"; 
				}
				textOutput += "\n"; 
			}
			Debug.Log(textOutput);
		}

		// splits a CSV row 
		static public string[] SplitCsvLine(string line)
		{
			// return (from System.Text.RegularExpressions.Match m in Regex.Matches(line,
			// @"(((?<x>(?=[\r\n]+))|""(?<x>([^""]|"""")+)""|(?<x>[^\r\n]+))\t?)", 
			// System.Text.RegularExpressions.RegexOptions.ExplicitCapture)
			// select m.Groups[1].Value).ToArray();
			return line.Split('\t');
		}
	
		// splits a CSV file into a 2D string array
		static public string[,] SplitCsvGrid(string csvText)
		{
			string[] lines = csvText.Split("\n"[0]); 
	
			// finds the max width of row
			int width = 0; 
			for (int i = 0; i < lines.Length; i++)
			{
				string[] row = SplitCsvLine( lines[i] ); 
				width = Mathf.Max(width, row.Length); 
			}
	
			// creates new 2D string grid to output to
			string[,] outputGrid = new string[width + 1, lines.Length + 1]; 
			for (int y = 0; y < lines.Length; y++)
			{
				string[] row = SplitCsvLine( lines[y] ); 
				for (int x = 0; x < row.Length; x++) 
				{
					outputGrid[x,y] = row[x]; 
	
					// This line was to replace "" with " in my output. 
					// Include or edit it as you wish.
					outputGrid[x,y] = outputGrid[x,y].Replace("\"\"", "\"");
				}
			}
	
			return outputGrid; 
		}

    }



}