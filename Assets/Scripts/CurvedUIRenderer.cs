using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
[AddComponentMenu("UI/Effects/Curved UI Renderer")]
public class CurvedUIRenderer : BaseMeshEffect
{
    [Tooltip("Promień cylindra w pikselach. MNIEJSZA WARTOŚĆ = BARDZIEJ ZAGIĘTY EKRAN (np. 350 - 600)")]
    [Range(150f, 1500f)]
    public float curveRadius = 450f;

    [Tooltip("Liczba podziałów poziomej siatki dla płynnego łuku")]
    [Range(4, 32)]
    public int horizontalSegments = 16;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0 || curveRadius <= 10f)
            return;

        // Jeśli to standardowy prostokąt UI (4 wierzchołki), generujemy gęstą siatkę pod zakrzywienie
        if (vh.currentVertCount == 4)
        {
            UIVertex v0 = new UIVertex();
            UIVertex v1 = new UIVertex();
            UIVertex v2 = new UIVertex();
            UIVertex v3 = new UIVertex();

            vh.PopulateUIVertex(ref v0, 0); // Bottom-Left
            vh.PopulateUIVertex(ref v1, 1); // Top-Left
            vh.PopulateUIVertex(ref v2, 2); // Top-Right
            vh.PopulateUIVertex(ref v3, 3); // Bottom-Right

            vh.Clear();

            int segments = Mathf.Max(2, horizontalSegments);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;

                // Wierzchołek dolny
                UIVertex bVert = LerpVertex(v0, v3, t);
                CurveSingleVertex(ref bVert);
                vh.AddVert(bVert);

                // Wierzchołek górny
                UIVertex tVert = LerpVertex(v1, v2, t);
                CurveSingleVertex(ref tVert);
                vh.AddVert(tVert);
            }

            for (int i = 0; i < segments; i++)
            {
                int bl = i * 2;
                int tl = bl + 1;
                int br = bl + 2;
                int tr = bl + 3;

                vh.AddTriangle(bl, tl, tr);
                vh.AddTriangle(bl, tr, br);
            }
        }
        else
        {
            // Dla czcionek TMP i złożonych siatek
            UIVertex vertex = new UIVertex();
            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                CurveSingleVertex(ref vertex);
                vh.SetUIVertex(vertex, i);
            }
        }
    }

    private void CurveSingleVertex(ref UIVertex vertex)
    {
        float x = vertex.position.x;
        float theta = x / curveRadius;

        vertex.position.x = Mathf.Sin(theta) * curveRadius;
        // W Unity Canvas -Z idzie w stronę kamery, więc zaginamy krawędzie ku oczom
        vertex.position.z -= (1.0f - Mathf.Cos(theta)) * curveRadius;
    }

    private UIVertex LerpVertex(UIVertex a, UIVertex b, float t)
    {
        UIVertex result = new UIVertex
        {
            position = Vector3.Lerp(a.position, b.position, t),
            color = Color32.Lerp(a.color, b.color, t),
            uv0 = Vector2.Lerp(a.uv0, b.uv0, t),
            uv1 = Vector2.Lerp(a.uv1, b.uv1, t),
            normal = Vector3.Lerp(a.normal, b.normal, t),
            tangent = Vector4.Lerp(a.tangent, b.tangent, t)
        };
        return result;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic != null) graphic.SetVerticesDirty();
    }
#endif
}