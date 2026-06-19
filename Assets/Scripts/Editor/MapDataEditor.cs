// ============================================================================
// MapDataEditor.cs — Custom inspector to paint map tiles visually!
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapData))]
public class MapDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapData map = (MapData)target;

        // Draw the default inspector (Name, Width, Height, Start/Goal positions)
        DrawDefaultInspector();

        GUILayout.Space(20);
        GUILayout.Label("🎨 Visual Map Designer", EditorStyles.boldLabel);

        if (GUILayout.Button("Initialize / Reset Grid Array"))
        {
            map.InitializeDefault();
            EditorUtility.SetDirty(map);
        }

        if (map.tileData == null || map.tileData.Length != map.width * map.height)
        {
            EditorGUILayout.HelpBox("Tile Data length is incorrect. Click Initialize to fix it.", MessageType.Warning);
            return;
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Click the tiles below to cycle through types:\nGrey = Passable\nBrown [W] = Wall\nYellow [C] = Collectible\nGreen [G] = Goal", MessageType.Info);
        GUILayout.Space(10);

        // Draw grid (z goes from top to bottom so it looks correct on screen)
        for (int z = map.height - 1; z >= 0; z--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // Center the grid
            
            for (int x = 0; x < map.width; x++)
            {
                int index = z * map.width + x;
                TileType currentType = (TileType)map.tileData[index];

                Color bgColor = Color.white;
                string label = "";

                switch (currentType)
                {
                    case TileType.Passable: bgColor = new Color(0.8f, 0.8f, 0.8f); label = " "; break;
                    case TileType.Wall: bgColor = new Color(0.4f, 0.2f, 0.1f); label = "W"; break;
                    case TileType.Collectible: bgColor = Color.yellow; label = "C"; break;
                    case TileType.Goal: bgColor = Color.green; label = "G"; break;
                }

                // Check if this is the start position
                if (map.robotStartPosition.x == x && map.robotStartPosition.y == z)
                {
                    label = "🤖" + label;
                }

                GUI.backgroundColor = bgColor;
                if (GUILayout.Button(label, GUILayout.Width(40), GUILayout.Height(40)))
                {
                    // Cycle to next type
                    int next = ((int)currentType + 1) % 4;
                    map.tileData[index] = next;
                    EditorUtility.SetDirty(map);
                }
                GUI.backgroundColor = Color.white;
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(map);
        }
    }
}
#endif
