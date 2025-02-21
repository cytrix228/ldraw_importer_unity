﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LDraw
{
    public class LDrawConfig : ScriptableObject
    {
        [SerializeField] private string _BasePartsPath;
       
        [SerializeField] private string _ModelsPath;
		public string ModelsPath {
			get { return _ModelsPath; }
		}

        [SerializeField] private string _ColorConfigPath;
        [SerializeField] private string _MaterialsPath;
        [SerializeField] private string _MeshesPath;
        [SerializeField] private float _Scale;
        [SerializeField] private Material _DefaultOpaqueMaterial;
        [SerializeField] private Material _DefaultTransparentMaterial;
        private Dictionary<string, string> _Parts;
        private Dictionary<string, string> _Models;
        
        private Dictionary<int, Material> _MainColors;
        private Dictionary<string, Material> _CustomColors;
        private Dictionary<string, string> _ModelFileNames;
        public Matrix4x4 ScaleMatrix
        {
            get { return Matrix4x4.Scale(new Vector3(_Scale, _Scale, _Scale)); }
        }

        public Material GetColoredMaterial(int code)
        {
            return _MainColors[code];
        }
        public Material GetColoredMaterial(string colorString)
        {
            if (_CustomColors.ContainsKey(colorString))
                return _CustomColors[colorString];
            var path = _MaterialsPath + colorString + ".mat";
            if (File.Exists(path))
            {
                _CustomColors.Add(colorString, AssetDatabase.LoadAssetAtPath<Material>(path));
            }
            else
            {
                var mat = new Material(_DefaultOpaqueMaterial);
                 
                mat.name = colorString;
                Color color;
                if (ColorUtility.TryParseHtmlString(colorString, out color))
                    mat.color = color;
                            
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
                _CustomColors.Add(colorString, mat);
            }

            return _CustomColors[colorString];
        }
        public string[] ModelFileNames
        {
            get { return _ModelFileNames.Keys.ToArray(); }
        }

        public string GetModelByFileName(string modelFileName)
        {
            return _ModelFileNames[modelFileName];
        }
        public string GetSerializedPart(string name)
        {
            try
            {
                name = name.ToLower();

                // replace '\' with '/'
                name = name.Replace('\\', '/');

                //Debug.Log("GetSerializedPart : " + name);

                //var serialized = _Parts.ContainsKey(name) ? File.ReadAllText(_Parts[name]) : _Models[name]; 
				Console.WriteLine( "Get Serialized Part or Model : " + name );
                if (_Parts.ContainsKey(name))
                {
                    //Debug.Log( "Reading part : " + _Parts[name] );
                    return File.ReadAllText(_Parts[name]);
                }
                else if (_Models.ContainsKey(name))
                {
                    //Debug.Log( "Reading model : " + _Models[name] );
                    return _Models[name];
                }
                // throw exception of "Name is not found"
                else
                {
                    Debug.Log("http://www.ldraw.org/library/tracker/");
                    EditorUtility.DisplayDialog("Error!", "Missing part or wrong part [" + name
                                                        + "]   ! Find it in url from debug console", "Ok", "");
                    throw new Exception("Name is not found");

                }
            }
            catch
            {
                Debug.Log("http://www.ldraw.org/library/tracker/");
                EditorUtility.DisplayDialog("Error!", "Missing part or wrong part [" + name
                                                        + "]   ! Find it in url from debug console", "Ok", "");
                throw;
            }
        
        }

        public void InitParts()
        { 
            PrepareModels();
            ParseColors();
            _Parts = new Dictionary<string, string>();

            var files_p = Directory.GetFiles(_BasePartsPath + "p/", "*.*", SearchOption.AllDirectories);
            var files_parts = Directory.GetFiles(_BasePartsPath + "parts/", "*.*", SearchOption.AllDirectories);

            foreach (var file in files_p)
            {
                if (!file.Contains(".meta"))
                {
                    string partsPath = _BasePartsPath + "p/";
                    //if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows) {
                    //    partsPath = partsPath.Replace('/', '\\');
                    //}
                    string fileName = file.Replace(partsPath, "").Split('.')[0];
                    fileName = fileName.Replace('\\', '/');
                    //Debug.Log("Part : " + file + "  Base " + partsPath + "  -> fileName : " + fileName );

                    //if (fileName.Contains("\\"))
                    //   fileName = fileName.Split('\\')[1]; 
                    fileName = fileName.ToLower();
                    if (!_Parts.ContainsKey(fileName))
                        _Parts.Add(fileName, file);
                }
            }
            foreach (var file in files_parts)
            {
                if (!file.Contains(".meta"))
                {
                    string partsPath = _BasePartsPath + "parts/";
                    //if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
                    //    partsPath = partsPath.Replace('/', '\\');
                    string fileName = file.Replace(partsPath, "").Split('.')[0];
                    fileName = fileName.Replace('\\', '/');
                    //Debug.Log("Part : " + file + "  Base " + partsPath + "  -> fileName : " + fileName);

                    //if (fileName.Contains("\\"))
                    //   fileName = fileName.Split('\\')[1];
                    fileName = fileName.ToLower();
                    if (!_Parts.ContainsKey(fileName))
                        _Parts.Add(fileName, file);
                }
            }

			Debug.Log("Parts loaded: " + _Parts.Count);
			//for( int i = 0; i < _Parts.Count; i++ ) {
			//	Debug.Log( "Part " + i + " : " + _Parts.ElementAt(i).Key );
			//}
        }

        private void ParseColors()
        {
            _MainColors = new Dictionary<int, Material>();
            using (StreamReader reader = new StreamReader(_ColorConfigPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
                    line = regex.Replace(line, " ").Trim();
                    var args = line.Split(' ');
                    if (args.Length  > 1 && args[1] == "!COLOUR")
                    {
                        var path =_MaterialsPath + args[2] + ".mat";
                        if (File.Exists(path))
                        {
                            _MainColors.Add(int.Parse(args[4]), AssetDatabase.LoadAssetAtPath<Material>(path));
                        }
                        else
                        {
                            Color color;
                            if (ColorUtility.TryParseHtmlString(args[6], out color))
                            {
                                int alphaIndex = Array.IndexOf(args, "ALPHA");
                                var mat = new Material(alphaIndex  > 0? _DefaultTransparentMaterial : _DefaultOpaqueMaterial);
                                mat.name = args[2];
                                mat.color = alphaIndex > 0? new Color(color.r, color.g, color.b, int.Parse(args[alphaIndex + 1]) / 256f) 
                                    : color;
                            
                                AssetDatabase.CreateAsset(mat, path);
                                _MainColors.Add(int.Parse(args[4]), mat);
                            }
                        }
                    
                    }
                }
                AssetDatabase.SaveAssets();
            }
        }
        
        private void PrepareModels()
        {
            _ModelFileNames = new Dictionary<string, string>();
            var files = Directory.GetFiles(_ModelsPath, "*.*", SearchOption.AllDirectories);

			
			// Console.WriteLine("  >> PrepareModels : \n" + files.Length + " files found.");
			// foreach( string file in files ) {
			// 	Console.WriteLine("File : " + file);
			// }
			

            _Models = new Dictionary<string, string>();
            foreach (var file in files)
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    string line;
                    string filename = String.Empty;

                    bool isFirst = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
                        line = regex.Replace(line, " ").Trim();
                        var args = line.Split(' ');
                        if (args.Length  > 1 && args[1] == "FILE")
                        {
                           
                            filename = GetFileName(args, 2);
							filename = filename.ToLower();
							
                            if (isFirst)
                            {
                                _ModelFileNames.Add(Path.GetFileNameWithoutExtension(file), filename);
                                isFirst = false;

								Console.WriteLine("Adding first model : " + Path.GetFileNameWithoutExtension(file) + " : " + filename);
                            }
                            
                            if(_Models.ContainsKey(filename))
                                filename = String.Empty;
                            else {
								Console.WriteLine("    Adding sub-model : " + filename);
                                _Models.Add(filename, String.Empty);
							}
                        }

                        if (!string.IsNullOrEmpty(filename))
                        {
                            _Models[filename] += line + "\n";
                        }
                    } 
                }
                
            }
        }

        public Mesh GetMesh(string name)
        {
            var path = Path.Combine(_MeshesPath, name + ".asset");
			path = path.Replace( '\\', '/' );
//			Debug.Log( " > Get Mesh path : " + path + " File exists ? " + File.Exists(path) );
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<Mesh>(path) : null;
        }

		
        public void SaveMesh(Mesh mesh)
        {
            var path = _MeshesPath;
			//Debug.Log( "Mesh path : " + path );
            path = Path.Combine(path, mesh.name + ".asset");
			path = path.Replace( '\\', '/' );
//			Debug.Log( " > Save Mesh path : " + path );


			// get the full path directory excluding the file name
			var path_dir = Path.GetDirectoryName( path );


            if (!Directory.Exists(path_dir))
            {
				//Debug.Log( "Creating directory: " + path_dir );
                Directory.CreateDirectory(path_dir);
            }

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }
        public static string GetFileName(string[] args, int filenamePos)
        {
            string name = string.Empty;
            for (int i = filenamePos; i < args.Length; i++)
            {
                name += args[i] + ' ';
            }
            name.Trim();
            //name = Path.GetFileNameWithoutExtension(name).ToLower();
            //Debug.Log("GetFileName2 : " + name);
            name = name.Replace('\\', '/');
//            Debug.Log("GetFileName : [" + name + "]");

            return name.Split('.')[0];
        }
        public static string GetExtension(string[] args, int filenamePos)
        {
            string name = string.Empty;
            for (int i = filenamePos; i < args.Length; i++)
            {
                name += args[i] + ' ';
            }
         
            return Path.GetExtension(name).Trim();
        }
        private static LDrawConfig _Instance;

        public static LDrawConfig Instance
        {
            get
            {
            	//Console.WriteLine("Find all : ");
            	//foreach( ScriptableObject _to in Resources.FindObjectsOfTypeAll(typeof(ScriptableObject)) as ScriptableObject[]) {
            //		Console.WriteLine(_to.name);
            	//}
                if (_Instance == null)
                {
                	Console.WriteLine("Load resource : " + ConfigPath);
                    //_Instance = Resources.Load<LDrawConfig>(ResourcePath);
					_Instance = AssetDatabase.LoadAssetAtPath<LDrawConfig>(ConfigPath);
                }

                return _Instance;
            }
        }

		public static LDrawConfig GetInstance()
		{
			Console.WriteLine("LDrawConfig::GetInstance() : " + LDrawConfig.Instance);
			return Instance;
		}

        private void OnEnable()
        {
        	Console.WriteLine("LDrawConfig::OnEnable()" + LDrawConfig.Instance);
            InitParts();
        }

        private const string ConfigPath = "Assets/Resources/Config.asset";
        private const string ResourcePath = "Config";
        public const int DefaultMaterialCode = 16;
    }
}
