using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class LaunchDemo
{
    Mesh celestialSphere;
    void BuildCelestialBodies()
    {
        celestialSphere=CreateCelestialSphere();
        earthRenderer.GetComponent<MeshFilter>().sharedMesh=celestialSphere;
        earthRenderer.sharedMaterial=PlanetMaterial("Earth • ocean, terrain and weather",CreateEarthMap(),.42f);
        earthRenderer.transform.rotation=Quaternion.Euler(12,105,-23.4f);
        var air=new GameObject("Blue atmospheric limb",typeof(MeshFilter),typeof(MeshRenderer));
        air.transform.SetParent(earthRenderer.transform,false);
        air.transform.localScale=Vector3.one*1.012f;
        air.GetComponent<MeshFilter>().sharedMesh=celestialSphere;
        var atmosphere=new Material(Resources.Load<Shader>("AuroraAtmosphere"));
        atmosphere.SetColor("_Color",new Color(.09f,.42f,1f,.45f));
        air.GetComponent<Renderer>().sharedMaterial=atmosphere;
        air.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        generatedResources.Add(atmosphere);

        moon=Make("Lunar destination",PrimitiveType.Sphere,Vector3.zero,Vector3.one*145,Color.white);
        Destroy(moon.GetComponent<Collider>());
        moon.GetComponent<MeshFilter>().sharedMesh=celestialSphere;
        moon.GetComponent<Renderer>().sharedMaterial=PlanetMaterial("Moon • cratered regolith",CreateMoonMap(),.06f);
    }

    Material PlanetMaterial(string label,Texture2D map,float smoothness)
    {
        var material=new Material(FindLitShader()) { name=label, color=Color.white };
        material.SetTexture("_BaseMap",map);
        material.SetFloat("_Smoothness",smoothness);
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap",map);
        material.SetColor("_EmissionColor",new Color(.045f,.065f,.11f));
        generatedResources.Add(material);
        return material;
    }

    Mesh CreateCelestialSphere()
    {
        const int longitude=128,latitude=64;
        var vertices=new Vector3[(longitude+1)*(latitude+1)];
        var normals=new Vector3[vertices.Length];
        var uv=new Vector2[vertices.Length];
        var triangles=new int[longitude*latitude*6];
        int t=0;
        for(int y=0;y<=latitude;y++) for(int x=0;x<=longitude;x++)
        {
            int i=y*(longitude+1)+x;
            float u=(float)x/longitude,v=(float)y/latitude;
            normals[i]=GlobePoint(u,v); vertices[i]=normals[i]*.5f; uv[i]=new Vector2(u,v);
            if(y==latitude || x==longitude) continue;
            int next=i+longitude+1;
            triangles[t++]=i; triangles[t++]=next; triangles[t++]=i+1;
            triangles[t++]=i+1; triangles[t++]=next; triangles[t++]=next+1;
        }
        var mesh=new Mesh { name="Smooth celestial sphere",vertices=vertices,normals=normals,uv=uv,triangles=triangles };
        mesh.RecalculateBounds(); mesh.RecalculateTangents(); generatedResources.Add(mesh); return mesh;
    }

    static Vector3 GlobePoint(float u,float v)
    {
        float lon=u*Mathf.PI*2,lat=(v-.5f)*Mathf.PI;
        return new Vector3(Mathf.Cos(lat)*Mathf.Cos(lon),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Sin(lon));
    }

    static float SmoothRange(float min,float max,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(min,max,value));

    static float GlobeNoise(Vector3 p,float scale)
    {
        p=p*scale+new Vector3(13.4f,7.8f,24.1f);
        return (Mathf.PerlinNoise(p.x,p.y)+Mathf.PerlinNoise(p.y,p.z)+Mathf.PerlinNoise(p.z,p.x))/3f;
    }

    Texture2D CreateEarthMap()
    {
        const int width=1024,height=512;
        var colors=new Color[width*height];
        // Stylized continental masses in longitude/latitude, with spherical coastal detail.
        Vector4[] continents={new Vector4(-112,48,36,27),new Vector4(-83,24,20,15),new Vector4(-62,-10,21,23),
            new Vector4(-69,-37,12,25),new Vector4(18,8,23,32),new Vector4(25,-20,15,18),
            new Vector4(15,52,24,16),new Vector4(66,49,50,24),new Vector4(110,35,32,22),
            new Vector4(78,17,10,18),new Vector4(111,-3,26,9),new Vector4(135,-26,24,15),new Vector4(-42,72,17,12)};
        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
        {
            float u=x/(float)(width-1),v=y/(float)(height-1),lat=(v-.5f)*180,lon=u*360-180;
            Vector3 p=GlobePoint(u,v);
            float n=GlobeNoise(p,12),fine=GlobeNoise(p,63),land=-10;
            foreach(var c in continents)
            {
                float dx=Mathf.DeltaAngle(c.x,lon)/c.z,dy=(lat-c.y)/c.w;
                land=Mathf.Max(land,1-dx*dx-dy*dy+(n-.5f)*1.8f+(fine-.5f)*.3f);
            }
            Color water=Color.Lerp(new Color(.008f,.035f,.13f),new Color(.025f,.28f,.43f),SmoothRange(-.5f,.12f,land));
            float desert=Mathf.Exp(-Mathf.Pow((Mathf.Abs(lat)-24)/13,2))*SmoothRange(.40f,.62f,n);
            Color ground=Color.Lerp(new Color(.07f,.24f,.13f),new Color(.65f,.48f,.25f),desert);
            ground*=Mathf.Lerp(.72f,1.2f,fine);
            Color color=Color.Lerp(water,ground,SmoothRange(-.04f,.08f,land));
            float ice=SmoothRange(68,86,Mathf.Abs(lat)+(n-.5f)*18);
            color=Color.Lerp(color,new Color(.84f,.93f,.97f),ice);
            float weather=GlobeNoise(p+new Vector3(Mathf.Sin(lat*.09f)*.12f,0,0),22)*.7f+GlobeNoise(p,48)*.3f;
            float clouds=SmoothRange(.53f,.68f,weather)*.88f;
            colors[y*width+x]=Color.Lerp(color,new Color(.93f,.96f,1),clouds);
        }
        return PlanetTexture("Earth continental atlas",width,height,colors);
    }

    Texture2D CreateMoonMap()
    {
        const int width=512,height=256,count=85;
        var random=new System.Random(1972);
        var centers=new Vector3[count]; var radii=new float[count];
        for(int i=0;i<count;i++)
        {
            centers[i]=GlobePoint((float)random.NextDouble(),.12f+(float)random.NextDouble()*.76f);
            radii[i]=.018f+Mathf.Pow((float)random.NextDouble(),2)*.19f;
        }
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
        {
            Vector3 p=GlobePoint(x/(float)(width-1),y/(float)(height-1));
            float maria=GlobeNoise(p,4),grain=GlobeNoise(p,90);
            float value=.31f+SmoothRange(.38f,.61f,maria)*.31f+(grain-.5f)*.16f;
            for(int c=0;c<count;c++)
            {
                float r=Vector3.Distance(p,centers[c])/radii[c];
                if(r>1.35f) continue;
                float rim=Mathf.Exp(-Mathf.Pow((r-1)*10,2))*.19f;
                float bowl=(1-SmoothRange(.5f,.96f,r))*.16f;
                value+=rim-bowl;
            }
            pixels[y*width+x]=new Color(value*.95f,value*.96f,value);
        }
        return PlanetTexture("Lunar maria and impact craters",width,height,pixels);
    }

    Texture2D PlanetTexture(string label,int width,int height,Color[] pixels)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,false) { name=label,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4 };
        texture.SetPixels(pixels); texture.Apply(true,true); generatedResources.Add(texture); return texture;
    }
}

