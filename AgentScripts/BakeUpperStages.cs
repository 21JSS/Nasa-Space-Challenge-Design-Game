using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Recorta el modelo original en tres volúmenes; conserva sus posiciones, UV y silueta.
public static class BakeUpperStages
{
    struct Vertex { public Vector3 p,n; public Vector2 uv; public static Vertex Lerp(Vertex a,Vertex b,float t) => new Vertex {p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)}; }
    public static string Run()
    {
        var model=Resources.Load<GameObject>("Modelado Cohete Separado"); Mesh source=null;
        foreach(var f in model.GetComponentsInChildren<MeshFilter>(true)) if(f.name.Contains("Modelo 4")) source=f.sharedMesh;
        if(source==null) throw new Exception("No se encuentra la sección superior.");
        string folder="Assets/LaunchPrototype/Resources/StageParts";
        System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var p=source.vertices; var n=source.normals; var uv=source.uv; var triangles=source.triangles;
        float z0=source.bounds.min.z, z1=source.bounds.max.z;
        float[] bounds={z0-0.001f,Mathf.Lerp(z0,z1,0.36f),Mathf.Lerp(z0,z1,0.70f),z1+0.001f};
        string[] names={"ICPS","ServiceModule","CrewCabin"};
        for(int stage=0;stage<3;stage++)
        {
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                var poly=new List<Vertex>();
                for(int j=0;j<3;j++) { int k=triangles[i+j]; poly.Add(new Vertex {p=p[k],n=n.Length>k?n[k]:Vector3.up,uv=uv.Length>k?uv[k]:Vector2.zero}); }
                poly=Clip(poly,bounds[stage],true); poly=Clip(poly,bounds[stage+1],false);
                for(int j=1;j+1<poly.Count;j++) foreach(int k in new[]{0,j,j+1}) { indices.Add(vertices.Count); vertices.Add(poly[k].p); normals.Add(poly[k].n); uvs.Add(poly[k].uv); }
            }
            var mesh=new Mesh {name=names[stage],indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uvs); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            string path=folder+"/"+names[stage]+".asset"; var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous==null) AssetDatabase.CreateAsset(mesh,path); else { EditorUtility.CopySerialized(mesh,previous); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        AssetDatabase.SaveAssets(); return "Tres piezas recortadas de Modelo 4: ICPS, servicio y cabina.";
    }
    static List<Vertex> Clip(List<Vertex> input,float plane,bool above)
    {
        var result=new List<Vertex>(); if(input.Count==0) return result;
        Vertex previous=input[input.Count-1]; bool prevInside=above?previous.p.z>=plane:previous.p.z<=plane;
        foreach(var current in input)
        {
            bool inside=above?current.p.z>=plane:current.p.z<=plane;
            if(inside!=prevInside) result.Add(Vertex.Lerp(previous,current,(plane-previous.p.z)/(current.p.z-previous.p.z)));
            if(inside) result.Add(current); previous=current; prevInside=inside;
        }
        return result;
    }
}
