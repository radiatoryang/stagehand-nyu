using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Stagehand
{

    public class StagehandCSV : MonoBehaviour
    {
        public static StagehandCSV Instance;

        public static string lastReport { get; private set; }

		public string downloadFromURL = "url";
		string urlData;
		public string[] writeToFilepaths = new string[8];
		string[,] csvData = new string[,] { {"C0", "C1", "C2", "C3", "C4", "C5", "C6", "C7"} };
		int currentRowIndex=0;

		void Start () {
			Application.targetFrameRate = 30;
			downloadFromURL = PlayerPrefs.GetString( "downloadFromURL", downloadFromURL);
			var newWriteToFilepaths = PlayerPrefs.GetString( "writeToFilepaths", ",,,,,,,").Split(new string[] {","}, System.StringSplitOptions.None );
			for ( int i=0; i<newWriteToFilepaths.Length; i++) {
				if ( !string.IsNullOrEmpty(newWriteToFilepaths[i]) ) {
					writeToFilepaths[i] = newWriteToFilepaths[i];
				}
			}
			StartRefresh();
		}

		void OnDisable() {
			PlayerPrefs.SetString( "downloadFromURL", downloadFromURL);
			PlayerPrefs.SetString( "writeToFilepaths", string.Join(",", writeToFilepaths) );
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
				StartVideoDownload(6, writeToFilepaths[6] );
			}
			GUI.enabled = true;
			downloadFromURL = GUILayout.TextField(downloadFromURL);
			// GUILayout.Label(" last report: " + lastReport);

			GUILayout.EndHorizontal();


			GUILayout.BeginHorizontal(skin.box);
			if ( downloadingVideos ) {
				GUILayout.Label("DOWNLOADING VIDEOS, please wait!... currently downloading: " + lastDownloadStatus);
			} else {
				if ( GUILayout.Button("-", GUILayout.MaxWidth(32)) ) {
					currentRowIndex = Mathf.Max(0, currentRowIndex - 1);
					RefreshToFiles();
				}
				GUILayout.Label( currentRowIndex.ToString(), GUILayout.MaxWidth(32) );
				int nextIndex = Mathf.Min(csvData.GetLength(1)-1, currentRowIndex + 1);
				if ( GUILayout.Button("+", GUILayout.MaxWidth(32)) ) {
					currentRowIndex = nextIndex;
					RefreshToFiles();
				}

				if ( csvData.GetLength(1) > 2 && Time.timeSinceLevelLoad > 1f) {
					GUILayout.FlexibleSpace();
					GUILayout.Label("NOW: " + csvData[1, currentRowIndex]);
					GUILayout.FlexibleSpace();
					GUILayout.Label("NEXT: " + csvData[1, nextIndex]);
				}
			}
			GUILayout.EndHorizontal();


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
			GUILayout.EndVertical();

			GUILayout.EndArea();
		}

		// downloads video files at all URLs in a column
		public void StartVideoDownload(int columnIndex, string folderpath) {
			if (downloadingVideos) {
				return;
			}
			StartCoroutine( VideoDownloadCoroutine(columnIndex, folderpath) );
		}

		bool downloadingVideos = false;
		IEnumerator VideoDownloadCoroutine(int columnIndex, string folderpath) {
			downloadingVideos = true;

			// gather video URLs
			var allVideoURLs = new string[csvData.GetLength(1)];
			for (int i=0; i<allVideoURLs.Length; i++) {
				allVideoURLs[i] = csvData[columnIndex, i];
			}

			// now download each video URL
			for( int i=0; i<allVideoURLs.Length; i++) {
				if ( string.IsNullOrWhiteSpace(allVideoURLs[i]) ) {
					continue;
				}
				string url = ProcessGoogleDriveURL( allVideoURLs[i] );
				if ( url == allVideoURLs[i] ) {
					// Debug.Log(i.ToString() + ". " + csvData[1, i] + ": not a google drive video link?" );
					continue; // bail out, the link is empty or it isn't a google drive link? idk
				}

				string filepath = folderpath + csvData[1, i] + ".mp4"; // TODO: how to know the file extension?
				lastDownloadStatus = i.ToString() + ". " + csvData[1, i] + ": SAVING VIDEO... " + url;
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
				// var bytes = web.downloadHandler.data;
				
				// using (FileStream fls = new FileStream(filepath, FileMode.Create))
				// {
				// 	fls.Write(bytes, 0, bytes.Length);
				// }
				Debug.Log(i.ToString() + ". " + csvData[1, i] + ": DONE! " + filepath );

				web.Dispose();
			}


			downloadingVideos = false;
		}

        public void StartRefresh()
        {
            StartCoroutine(RefreshCoroutine(downloadFromURL));
        }

		void RefreshToFiles() {
			int maxX = csvData.GetUpperBound(0);
			string line = "";
			for ( int x=0; x<maxX; x++) {
				line += x.ToString() + ": " + csvData[x, currentRowIndex] + " | ";
			}
			Debug.Log(line);

			for (int i=0; i<writeToFilepaths.Length; i++ ) {
				if ( string.IsNullOrEmpty(writeToFilepaths[i]) ) {
					continue;
				}
				if ( writeToFilepaths[i].EndsWith(".mp4") ) {
					// swap in alternate files instead of downloading?
					continue;
				}
				if ( SaveIfImage( csvData[i, currentRowIndex], writeToFilepaths[i] ) ) {
					continue;
				}
				StreamWriter writer = new StreamWriter(writeToFilepaths[i], false);
				string data = csvData[i, currentRowIndex];
				if ( data.StartsWith("https://") ) { 
					data = data.Substring(8);
				}
				if ( data.Length <= 4 && (data.StartsWith("BFA") || data.StartsWith("MFA")) ) {
					data = data.Substring(0, 3);
				}
				writer.WriteLine(data);
				writer.Close();
			}
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
				lastReport = string.Format("\n- [ERROR] {0}: {1}", url, results.error);
				yield break;
			}

			// make sure the fetched file isn't just a Google login page
			if (results.downloadHandler.text.Contains("google-site-verification")) {
				// Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}> because the Google Doc didn't have public link sharing enabled", results.url);
				lastReport = string.Format("\n- [ERROR] {0}: This Google Docs share link does not have 'VIEW' access; make sure you enable link sharing.", url);
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