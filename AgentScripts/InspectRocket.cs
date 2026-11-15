using System.Linq;
using UnityEngine;
public static class InspectRocket
{
    public static object Run()
    {
        var model=Resources.Load<GameObject>("Modelado Cohete Separado");
        return model.GetComponentsInChildren<MeshFilter>(true).Select(f=>new { f.name, vertices=f.sharedMesh.vertexCount, bounds=f.sharedMesh.bounds.ToString(), scale=f.transform.lossyScale.ToString(), position=f.transform.position.ToString(),readable=f.sharedMesh.isReadable }).ToArray();
    }
}
