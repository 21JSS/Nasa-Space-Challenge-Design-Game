using Artemis;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// Visual ocean and recovery choreography. Flight telemetry stays owned by Mision.
public sealed partial class LaunchDemo
{
    Material recoveryWaterMaterial, recoverySkyMaterial;
    Material recoveryOrangeFabric, recoveryIvoryFabric, recoveryCord;
    Mesh recoveryOceanMesh;
    GameObject recoverySky;
    ParticleSystem recoverySpray;
    Transform[] recoveryCanopies;
    Vector3[] recoveryCanopyCenters, canopyReleasePositions;
    Quaternion[] canopyReleaseRotations;
    Mesh recoveryCordsMesh;
    Vector3[] recoveryCordVertices;
    bool canopiesReleased;
    float recoveryTime, splashdownAge = -1;
    bool recoveryPaused;
    public bool PresentationPaused => Simulation != null && (Simulation.Pausada || recoveryPaused);
    Vector3 splashPosition;
    static readonly int RecoveryTimeId = Shader.PropertyToID("_RecoveryTime");
    static readonly int CapsulePositionId = Shader.PropertyToID("_CapsulePosition");
    static readonly int SplashPositionId = Shader.PropertyToID("_SplashPosition");
    static readonly int SplashAgeId = Shader.PropertyToID("_SplashAge");
    float RecoverySeaLevel => 4.2f + crewCabin.localPosition.y - .33f;

    void BuildRecoveryEnvironment()
    {
        var noise=CreateRecoveryNoise();
        generatedResources.Add(noise);
        recoveryWaterMaterial=new Material(Resources.Load<Shader>("AuroraOcean")) { name="Pacific / moving swells and foam" };
        recoverySkyMaterial=new Material(Resources.Load<Shader>("AuroraRecoverySky")) { name="Pacific / cloudscape" };
        recoveryWaterMaterial.SetTexture("_NoiseTex",noise);
        recoverySkyMaterial.SetTexture("_NoiseTex",noise);
        generatedResources.Add(recoveryWaterMaterial); generatedResources.Add(recoverySkyMaterial);

        // Dense around the capsule; progressively coarser toward a 6 km horizon.
        const int cells=96;
        var vertices=new Vector3[(cells+1)*(cells+1)];
        var triangles=new int[cells*cells*6];
        for(int z=0;z<=cells;z++) for(int x=0;x<=cells;x++)
        {
            float u=(x/(float)cells*2-1),v=(z/(float)cells*2-1);
            vertices[z*(cells+1)+x]=new Vector3(u*u*u*6000,0,v*v*v*6000);
            if(x==cells || z==cells) continue;
            int t=(z*cells+x)*6,i=z*(cells+1)+x;
            triangles[t]=i; triangles[t+1]=i+cells+1; triangles[t+2]=i+1;
            triangles[t+3]=i+1; triangles[t+4]=i+cells+1; triangles[t+5]=i+cells+2;
        }
        recoveryOceanMesh=new Mesh { name="Pacific / adaptive ocean grid",vertices=vertices,triangles=triangles };
        recoveryOceanMesh.RecalculateNormals(); recoveryOceanMesh.RecalculateBounds();
        recoveryOceanMesh.bounds=new Bounds(Vector3.zero,new Vector3(12000,4,12000));
        generatedResources.Add(recoveryOceanMesh);
        ocean=new GameObject("Pacific splashdown",typeof(MeshFilter),typeof(MeshRenderer));
        ocean.transform.SetParent(transform,false);
        ocean.GetComponent<MeshFilter>().sharedMesh=recoveryOceanMesh;
        var waterRenderer=ocean.GetComponent<MeshRenderer>();
        waterRenderer.sharedMaterial=recoveryWaterMaterial;
        waterRenderer.shadowCastingMode=ShadowCastingMode.Off;
        waterRenderer.receiveShadows=false;

        recoverySky=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        recoverySky.name="Pacific / textured sky dome";
        recoverySky.transform.SetParent(transform,false);
        // In front of the distant globe, behind the vehicle and nearby ocean.
        recoverySky.transform.localScale=Vector3.one*2000;
        Destroy(recoverySky.GetComponent<Collider>());
        var skyRenderer=recoverySky.GetComponent<Renderer>();
        skyRenderer.sharedMaterial=recoverySkyMaterial;
        skyRenderer.shadowCastingMode=ShadowCastingMode.Off;
        skyRenderer.receiveShadows=false;
        BuildRecoverySpray();
        ResetRecoveryPresentation();
    }

    Texture2D CreateRecoveryNoise()
    {
        const int size=256;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true)
        { name="Pacific / seamless cloud and water detail",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4 };
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float u=x/(float)size,v=y/(float)size;
            float a=TileNoise(u,v,8),b=TileNoise(u,v,17),c=TileNoise(u,v,33);
            pixels[y*size+x]=new Color(a,b,c,1);
        }
        texture.SetPixels(pixels); texture.Apply(true,true); return texture;
    }
    static float TileNoise(float u,float v,float scale)
    {
        return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(u*scale+19,v*scale+37),Mathf.PerlinNoise((u-1)*scale+19,v*scale+37),u),
            Mathf.Lerp(Mathf.PerlinNoise(u*scale+19,(v-1)*scale+37),Mathf.PerlinNoise((u-1)*scale+19,(v-1)*scale+37),u),v);
    }

    void BuildRecoverySpray()
    {
        var go=new GameObject("Pacific / touchdown spray"); go.transform.SetParent(transform,false);
        recoverySpray=go.AddComponent<ParticleSystem>();
        recoverySpray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=recoverySpray.main;
        main.playOnAwake=false; main.loop=false; main.duration=1;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.8f);
        main.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,3.3f);
        main.startSize=new ParticleSystem.MinMaxCurve(.025f,.09f);
        main.startColor=new Color(.66f,.89f,.96f,.85f);
        main.gravityModifier=.65f; main.maxParticles=180;
        main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var emission=recoverySpray.emission; emission.enabled=false;
        var shape=recoverySpray.shape; shape.shapeType=ParticleSystemShapeType.Cone; shape.angle=60; shape.radius=.35f; shape.rotation=new Vector3(-90,0,0);
        var size=recoverySpray.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
        var color=recoverySpray.colorOverLifetime; color.enabled=true;
        var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(new Color(.66f,.89f,.96f),0),new GradientColorKey(new Color(.86f,.96f,1),1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)}); color.color=gradient;
        var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name="Pacific / spray droplets" };
        mat.SetFloat("_Surface",1); mat.SetFloat("_Blend",0); mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite",0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue=3000;
        mat.SetTexture("_BaseMap",starTexture); generatedResources.Add(mat);
        var renderer=recoverySpray.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=mat;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    void ResetRecoveryPresentation()
    {
        recoveryTime=0; splashdownAge=-1; recoveryPaused=false;
        canopiesReleased=false;
        if(ocean!=null) ocean.SetActive(false);
        if(recoverySky!=null) recoverySky.SetActive(false);
        if(recoverySpray!=null) recoverySpray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(recoveryWaterMaterial!=null) recoveryWaterMaterial.SetFloat(SplashAgeId,-1);
    }

    void UpdateRecoveryClock()
    {
        if(Simulation.Fase!=Fase.Aterrizaje && Simulation.Fase!=Fase.Regreso) return;
        bool frozen=WelcomeVisible || Simulation.Pausada || recoveryPaused;
        if(!frozen)
        {
            recoveryTime+=Time.deltaTime;
            if(splashdownAge>=0) splashdownAge+=Time.deltaTime;
        }
        if(recoverySpray!=null) { var main=recoverySpray.main; main.simulationSpeed=frozen?0:1; }
    }

    // Keep these waves synchronized with AuroraOcean.shader, including their phase velocities.
    static float RecoveryWave(float x,float z,float t)
    {
        return Mathf.Sin(x*.72f+z*.34f+t*1.25f)*.055f
            +Mathf.Sin(-x*1.1f+z*.91f-t*1.65f)*.028f
            +Mathf.Sin(x*2.1f+z*1.7f+t*2.1f)*.012f;
    }

    void ApplyRecoveryMotion()
    {
        if(Simulation.Fase!=Fase.Aterrizaje || crewCabin==null) return;
        var m=Simulation;
        bool landed=m.Resultado?.Tipo==TipoResultado.Exito;
        float t=recoveryTime;
        if(!landed && m.Resultado==null)
        {
            float supported=m.Principales?1:m.Drogues?.35f:0;
            float nearWater=Mathf.SmoothStep(0,1,Altitude/80f);
            rocket.position+=new Vector3(Mathf.Sin(t*.75f)*.22f,0,Mathf.Sin(t*.53f)*.12f)*supported*nearWater;
            if(supported>0)
            {
                Quaternion sway=Quaternion.Euler(Mathf.Sin(t*.95f)*2,0,Mathf.Sin(t*.75f)*3.8f);
                // Absolute attitude prevents accumulated drift while paused.
                crewCabin.rotation=Quaternion.Slerp(Quaternion.FromToRotation(Vector3.down,VisualFlightDirection),sway,supported);
            }
        }
        if(landed)
        {
            float x=rocket.position.x+Mathf.Sin(t*.4f)*.12f;
            float z=Mathf.Sin(t*.32f)*.10f;
            float wave=RecoveryWave(x,z,t);
            float settle=splashdownAge<0?0:Mathf.Sin(splashdownAge*5)*Mathf.Exp(-splashdownAge*1.4f)*.12f;
            rocket.position=new Vector3(x,4.2f+wave-.055f-settle,z);
            float dx=(RecoveryWave(x+.15f,z,t)-RecoveryWave(x-.15f,z,t))/.3f;
            float dz=(RecoveryWave(x,z+.15f,t)-RecoveryWave(x,z-.15f,t))/.3f;
            crewCabin.rotation=Quaternion.FromToRotation(Vector3.up,new Vector3(-dx,1,-dz).normalized)
                *Quaternion.Euler(Mathf.Sin(t*.8f)*1.2f,Mathf.Sin(t*.31f)*4,Mathf.Sin(t*.6f)*2);
        }
        AnimateRecoveryCanopies();
    }

    void AnimateRecoveryCanopies()
    {
        if(parachutes==null || recoveryCanopies==null) return;
        float t=recoveryTime;
        if(splashdownAge<0)
        {
            parachutes.transform.localPosition=Vector3.zero;
            parachutes.transform.rotation=Quaternion.Euler(Mathf.Sin(t*.65f)*1.8f,0,Mathf.Sin(t*.8f)*3.2f);
            if(!WelcomeVisible && !PresentationPaused)
            {
                float size=Simulation.Principales?1:.35f;
                float scale=Mathf.MoveTowards(parachutes.transform.localScale.x,size,Time.deltaTime*.45f);
                parachutes.transform.localScale=Vector3.one*scale;
            }
            for(int i=0;i<recoveryCanopies.Length;i++)
            {
                float phase=t*1.65f+i*2.1f;
                recoveryCanopies[i].localPosition=recoveryCanopyCenters[i]+new Vector3(Mathf.Sin(phase*.7f)*.08f,Mathf.Sin(phase)*.05f,Mathf.Cos(phase*.8f)*.06f);
                recoveryCanopies[i].localRotation=Quaternion.Euler(Mathf.Sin(phase)*3,0,Mathf.Cos(phase*.9f)*4);
                recoveryCanopies[i].localScale=new Vector3(1+Mathf.Sin(phase)*.018f,1+Mathf.Cos(phase)*.06f,1-Mathf.Sin(phase)*.018f);
            }
        }
        else
        {
            if(!canopiesReleased)
            {
                canopiesReleased=true;
                parachutes.transform.SetParent(transform,true);
                for(int i=0;i<recoveryCanopies.Length;i++)
                {
                    canopyReleasePositions[i]=recoveryCanopies[i].position;
                    canopyReleaseRotations[i]=recoveryCanopies[i].rotation;
                }
                parachutes.transform.localScale=Vector3.one;
            }
            bool submerged=true;
            for(int i=0;i<recoveryCanopies.Length;i++)
            {
                float age=Mathf.Max(0,splashdownAge-i*.35f);
                float falling=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/3.8f));
                float deflate=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/4.8f));
                // The wet fabric drifts independently, then descends continuously below the opaque water.
                float sink=Mathf.Max(0,age-6)*.17f;
                Vector3 landing=canopyReleasePositions[i]+new Vector3(1.05f+i*.24f,0,.65f+i*.18f);
                landing.y=RecoverySeaLevel+RecoveryWave(landing.x,landing.z,t)+.025f-sink;
                Vector3 p=Vector3.Lerp(canopyReleasePositions[i],landing,falling);
                p.x+=Mathf.Sin(t*.8f+i)*.09f*falling;
                recoveryCanopies[i].position=p;
                recoveryCanopies[i].rotation=Quaternion.Slerp(canopyReleaseRotations[i],Quaternion.Euler(Mathf.Sin(t+i)*5,i*17,Mathf.Cos(t*.7f+i)*5),falling);
                recoveryCanopies[i].localScale=new Vector3(1+deflate*.16f,Mathf.Lerp(1,.13f,deflate),1-deflate*.12f);
                submerged &= p.y<RecoverySeaLevel-.65f;
            }
            // Culling occurs only once every canopy is already hidden beneath the surface.
            if(submerged) parachutes.SetActive(false);
        }
        if(parachutes.activeSelf) UpdateRecoveryCords();
    }

    void BuildRecoveryCords()
    {
        const int cords=36;
        recoveryCordVertices=new Vector3[cords*8];
        var triangles=new int[cords*12];
        for(int i=0;i<cords;i++) for(int ribbon=0;ribbon<2;ribbon++)
        {
            int v=i*8+ribbon*4,q=i*12+ribbon*6;
            triangles[q]=v; triangles[q+1]=v+2; triangles[q+2]=v+1;
            triangles[q+3]=v+1; triangles[q+4]=v+2; triangles[q+5]=v+3;
        }
        recoveryCordsMesh=new Mesh { name="Recovery / batched suspension",vertices=recoveryCordVertices,triangles=triangles };
        recoveryCordsMesh.MarkDynamic(); generatedResources.Add(recoveryCordsMesh);
        var go=new GameObject("Recovery suspension / single mesh",typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(parachutes.transform,false);
        go.GetComponent<MeshFilter>().sharedMesh=recoveryCordsMesh;
        var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=recoveryCord;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
    }

    void UpdateRecoveryCords()
    {
        for(int canopy=0;canopy<3;canopy++) for(int line=0;line<12;line++)
        {
            float a=line*Mathf.PI/6;
            Vector3 endWorld=recoveryCanopies[canopy].TransformPoint(new Vector3(Mathf.Cos(a)*.88f,0,Mathf.Sin(a)*.88f));
            Vector3 startWorld=crewCabin.TransformPoint(new Vector3(0,.18f,0));
            if(canopiesReleased)
            {
                float release=Mathf.SmoothStep(0,1,Mathf.Clamp01(splashdownAge/2));
                Vector3 loose=recoveryCanopies[canopy].position+new Vector3(Mathf.Cos(a)*.35f,-.04f,Mathf.Sin(a)*.35f);
                startWorld=Vector3.Lerp(splashPosition+Vector3.up*.18f,loose,release);
            }
            Vector3 start=parachutes.transform.InverseTransformPoint(startWorld),end=parachutes.transform.InverseTransformPoint(endWorld);
            Vector3 direction=(end-start).normalized;
            Vector3 side=Vector3.Cross(direction,Vector3.forward).normalized*.004f;
            Vector3 cross=Vector3.Cross(direction,side).normalized*.004f;
            int v=(canopy*12+line)*8;
            recoveryCordVertices[v]=start-side; recoveryCordVertices[v+1]=start+side; recoveryCordVertices[v+2]=end-side; recoveryCordVertices[v+3]=end+side;
            recoveryCordVertices[v+4]=start-cross; recoveryCordVertices[v+5]=start+cross; recoveryCordVertices[v+6]=end-cross; recoveryCordVertices[v+7]=end+cross;
        }
        recoveryCordsMesh.vertices=recoveryCordVertices;
        recoveryCordsMesh.RecalculateBounds();
    }

    void UpdateRecoveryEnvironment()
    {
        if(ocean==null) return;
        if(launchGround!=null) launchGround.gameObject.SetActive(Simulation.Fase!=Fase.Aterrizaje && Simulation.Fase!=Fase.Regreso);
        bool returning=Simulation.Fase==Fase.Regreso;
        bool visible=(Simulation.Fase==Fase.Aterrizaje || returning && Altitude<40000) && !WelcomeVisible;
        ocean.SetActive(visible);
        float air=1-SmoothRange(18000,85000,Altitude);
        recoverySky.SetActive(!WelcomeVisible && air>.001f);
        recoverySky.transform.position=launchCamera.transform.position;
        recoverySkyMaterial.SetFloat("_AtmosphereBlend",air);
        recoverySkyMaterial.SetFloat(RecoveryTimeId,flightPresentationTime);
        if(!visible) return;
        ocean.transform.position=new Vector3(rocket.position.x,RecoverySeaLevel,rocket.position.z);
        recoverySky.transform.position=launchCamera.transform.position;
        if(Simulation.Resultado?.Tipo==TipoResultado.Exito && splashdownAge<0)
        {
            splashdownAge=0; splashPosition=new Vector3(crewCabin.position.x,RecoverySeaLevel,crewCabin.position.z);
            recoverySpray.transform.position=splashPosition;
            recoverySpray.Play(); recoverySpray.Emit(140);
        }
        recoveryWaterMaterial.SetFloat(RecoveryTimeId,recoveryTime);
        recoveryWaterMaterial.SetFloat(SplashAgeId,splashdownAge);
        recoveryWaterMaterial.SetVector(CapsulePositionId,new Vector4(crewCabin.position.x,crewCabin.position.z,Mathf.Clamp01(1-Altitude/5f),0));
        recoveryWaterMaterial.SetVector(SplashPositionId,new Vector4(splashPosition.x,splashPosition.z,0,0));
    }

    static Mesh CreateRecoveryCanopyMesh()
    {
        const int sides=72,rings=10;
        var vertices=new Vector3[(sides+1)*(rings+1)];
        var uv=new Vector2[vertices.Length];
        var orange=new List<int>(); var ivory=new List<int>();
        for(int r=0;r<=rings;r++) for(int s=0;s<=sides;s++)
        {
            float angle=s*Mathf.PI*2/sides,theta=Mathf.Lerp(.075f,Mathf.PI*.5f,r/(float)rings);
            float radius=Mathf.Sin(theta)*.88f*(1+.025f*Mathf.Cos(angle*24));
            int i=r*(sides+1)+s;
            vertices[i]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Cos(theta)*.57f,Mathf.Sin(angle)*radius);
            uv[i]=new Vector2(s/(float)sides,r/(float)rings);
            if(r==rings || s==sides) continue;
            var tris=s/3%2==0?orange:ivory;
            tris.Add(i); tris.Add(i+1); tris.Add(i+sides+1);
            tris.Add(i+1); tris.Add(i+sides+2); tris.Add(i+sides+1);
        }
        var mesh=new Mesh { name="Recovery / vented fabric canopy",vertices=vertices,uv=uv,subMeshCount=2 };
        mesh.SetTriangles(orange,0); mesh.SetTriangles(ivory,1); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
