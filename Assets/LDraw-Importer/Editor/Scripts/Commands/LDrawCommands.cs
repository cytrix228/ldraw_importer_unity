using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace LDraw
{
    
    public enum CommandType
    {
		PartDesc = 0,
        SubFile = 1,
        Triangle = 3,
        Quad = 4
    }

    public abstract class LDrawCommand
    {
        protected int _ColorCode = -1;
        protected string _Color;
        
        protected LDrawModel _Parent;
        public static LDrawCommand DeserializeCommand(string line, LDrawModel parent)
        {
            LDrawCommand command = null;
            int type;
            var args = line.Split(' ');

			// if( line.StartsWith("0") ) {
			//  	Debug.Log("line : " + line );


			// 	int type2;
			// 	bool ret = Int32.TryParse(args[0], out type2);
			// 	Debug.Log( "args[0] : " + args[0] + " type2 : " + type2 + "  " + ret );
				

			// }

            if (Int32.TryParse(args[0], out type))
            {
                var commandType = (CommandType)type;
             
                switch (commandType)
                {
					case CommandType.PartDesc:
						//  Debug.Log("PartDesc : " + line );
						//  if( args.Length >= 3 ) {
						// 	for( int i = 0; i < args.Length; i++ ) {
						// 		string debugmsg = "args[" + i + "] : " + args[i];
						// 		debugmsg += " " + (args[i] == "!LDRAW_ORG");
						// 		Debug.Log( debugmsg);
						// 	}
						//  }
						// debug args
                        if( args.Length >= 3 &&  args[1] == "!LDRAW_ORG" ) {
							//Debug.Log( ">>   !LDRAW_ORG" );
							if( args[2] == "Part") {
								//Debug.Log("New LDrawPart" );
								command = new LDrawPart();
							}
							else if( args[2] == "Subpart")
								command = new LDrawSubpart();
							else if( args[2] == "Primitive" || args[2] == "8_Primitive" || args[2] == "48_Primitive" )
								command = new LDrawPrimitive();
							else if( args[2] == "Shortcut")
								command = new LDrawShortcut();
						}
						break;
                    case CommandType.SubFile:
//						Debug.Log("SubFile : " + line );
                        command = new LDrawSubFile();
                        break;
                    case CommandType.Triangle:
                        command = new LDrawTriangle();
                        break;
                    case CommandType.Quad:
                        command = new LDrawQuad();
                        break;
                }
            }
           
            if (command != null)
            {
                if(!int.TryParse(args[1],out command._ColorCode))
                {
                    command._Color = args[1];
                }
                command._Parent = parent;
                command.Deserialize(line);
            }

			LDrawPart partComd = command as LDrawPart;

			if( partComd != null ) { 
				Debug.Log("command : " + command );
			}
            return command;
        }
        
        protected Vector3[] _Verts;
        public abstract void PrepareMeshData(List<int> triangles, List<Vector3> verts);
        public abstract void Deserialize(string serialized);

    }
}
