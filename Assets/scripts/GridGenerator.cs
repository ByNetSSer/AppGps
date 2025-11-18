using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public int gridSize = 10;
    public float cellSize = 1f;

    void Start()
    {
        CreateGrid();
    }

    void CreateGrid()
    {
        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineRenderer.endColor = new Color(1, 1, 1, 0.3f);
        lineRenderer.startWidth = lineRenderer.endWidth = 0.05f;

        Vector3[] positions = new Vector3[gridSize * 4];
        int index = 0;

        // Líneas horizontales
        for (int i = 0; i <= gridSize; i++)
        {
            float z = i * cellSize - (gridSize * cellSize) / 2;
            positions[index++] = new Vector3(-gridSize * cellSize / 2, 0.01f, z);
            positions[index++] = new Vector3(gridSize * cellSize / 2, 0.01f, z);
        }

        // Líneas verticales  
        for (int i = 0; i <= gridSize; i++)
        {
            float x = i * cellSize - (gridSize * cellSize) / 2;
            positions[index++] = new Vector3(x, 0.01f, -gridSize * cellSize / 2);
            positions[index++] = new Vector3(x, 0.01f, gridSize * cellSize / 2);
        }

        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);
    }
}
