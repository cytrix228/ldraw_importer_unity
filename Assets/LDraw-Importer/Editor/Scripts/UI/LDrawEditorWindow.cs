using System;
using UnityEditor;
using UnityEngine;

namespace LDraw
{
    public class LDrawEditorWindow : EditorWindow
    {
        [MenuItem("Window/LDrawImporter/Open Importer")]
        public static void Create()
        {
            var window = GetWindow<LDrawEditorWindow>("LDrawImporter");
            window.position = new Rect(100, 100, 400, 400);
            window.Show();
        }

        private string[] _ModelNames;
        private string _CurrentPart;
        private int _CurrentIndex = 0;
        private GeneratingType _CurrentType;

        private void OnEnable()
        {
            _ModelNames = LDrawConfig.Instance.ModelFileNames;
        }

        private void OnGUI()
        {
            GUILayout.Label("This is LDraw model importer for file format v1.0.2");
            if (GUILayout.Button("Update blueprints"))
            {
                LDrawConfig.Instance.InitParts();
                _ModelNames = LDrawConfig.Instance.ModelFileNames;
            }
            _CurrentType = (GeneratingType) EditorGUILayout.EnumPopup("Blueprint Type", _CurrentType);
            switch (_CurrentType)
            {
                    case GeneratingType.ByName:
                        _CurrentPart = EditorGUILayout.TextField("Name", _CurrentPart);
                        break;
                    case GeneratingType.Models:
                        _CurrentIndex = EditorGUILayout.Popup("Models", _CurrentIndex, _ModelNames);
                        break;
            }
      
            GenerateModelButton();
        }

        public static void GenerateSomeModel()
        {
			var window = GetWindow<LDrawEditorWindow>("LDrawImporter");
			Console.WriteLine("GenerateSomeModel window : " + window);
			Console.WriteLine("LDrawConfig.Instance : " + LDrawConfig.Instance);
			Console.WriteLine(" Model Path : " + LDrawConfig.Instance.ModelsPath);

			LDrawConfig.Instance.InitParts();
            window._ModelNames = LDrawConfig.Instance.ModelFileNames;
			foreach( var name in window._ModelNames)
			{
				Console.WriteLine("GenerateSomeModel name : " + name);
			}

            window._CurrentPart = LDrawConfig.Instance.GetModelByFileName(
				window._ModelNames[0]);
			
			Console.WriteLine("GenerateSomeModel window._CurrentPart : " + window._CurrentPart);

            // good test 949ac01
            var model = LDrawModel.Create(window._CurrentPart,
										 LDrawConfig.Instance.GetSerializedPart(window._CurrentPart));
            var go = model.CreateMeshGameObject(LDrawConfig.Instance.ScaleMatrix);
            go.transform.LocalReflect(Vector3.up);
     	
        }

        private void GenerateModelButton()
        {
            if (GUILayout.Button("Generate"))
            {
                _CurrentPart = _CurrentType == GeneratingType.ByName ? _CurrentPart 
                    : LDrawConfig.Instance.GetModelByFileName(_ModelNames[_CurrentIndex]); 
                // good test 949ac01
                var model = LDrawModel.Create(_CurrentPart, LDrawConfig.Instance.GetSerializedPart(_CurrentPart));
                var go = model.CreateMeshGameObject(LDrawConfig.Instance.ScaleMatrix);
                go.transform.LocalReflect(Vector3.up);
            }
			if(GUILayout.Button("Test Value")) {
				Debug.Log("Test Value");

				// get current selected object
				var selectedObject = Selection.activeGameObject;
				if(selectedObject != null) {
					Debug.Log("selectedObject : " + selectedObject);

					Debug.Log("selectedObject.transform.position : " + 
						selectedObject.transform.localToWorldMatrix );

				}
			}
        }
        private enum GeneratingType
        {
            ByName,
            Models
        }
        private const string PathToModels = "Assets/LDraw-Importer/Editor/base-parts/";
    }
}
