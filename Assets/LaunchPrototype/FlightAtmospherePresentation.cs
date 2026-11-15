using Artemis;
using UnityEngine;
using UnityEngine.Rendering;

// Visual motion cues share a pause-aware clock; all telemetry comes from Mision.
public sealed partial class LaunchDemo
{
    float flightPresentationTime, cloudTravel;
    ParticleSystem flightClouds;
    ParticleSystem.Particle[] flightCloudParticles;
    Vector3[] flightCloudSeeds;
    Material flightCloudMaterial;

    void UpdateFlightPresentationClock()
    {
        if(WelcomeVisible || PresentationPaused || Simulation.DecisionMotor || Simulation.ListaTli || Simulation.ListaReentrada) return;
        if(Simulation.Resultado!=null && Simulation.Resultado.Tipo!=TipoResultado.Exito) return;
        flightPresentationTime+=Time.deltaTime;
        cloudTravel+=Time.deltaTime*Mathf.Lerp(2,42,SmoothRange(0,1200,Speed));
    }

    Vector3 FlightCameraShake(bool firing)
    {
        float load=Simulation.Fase==Fase.Regreso && Simulation.Resultado==null?heatBlend*.035f:0;
        float strength=firing?Mathf.Lerp(.055f,.12f,SmoothRange(0,1200,Speed)):load;
        float t=flightPresentationTime;
        return new Vector3(Mathf.PerlinNoise(t*17,0)-.5f,Mathf.PerlinNoise(0,t*19)-.5f,0)*strength;
    }

    void BuildFlightClouds()
    {
        const int count=32;
        flightCloudParticles=new ParticleSystem.Particle[count];
        flightCloudSeeds=new Vector3[count];
        var random=new System.Random(41026);
        var go=new GameObject("Flight / passing cloud banks");
        go.transform.SetParent(transform,false);
        flightClouds=go.AddComponent<ParticleSystem>();
        flightClouds.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=flightClouds.main;
        main.playOnAwake=false; main.maxParticles=count; main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startSpeed=0; main.startLifetime=1000; main.startSize3D=true;
        var emission=flightClouds.emission; emission.enabled=false;
        var shape=flightClouds.shape; shape.enabled=false;
        flightCloudMaterial=new Material(Resources.Load<Shader>("AuroraExhaust")) { name="Flight / soft atmospheric clouds" };
        flightCloudMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        flightCloudMaterial.SetFloat("_Intensity",1);
        flightCloudMaterial.SetFloat("_UseEffectTime",1);
        generatedResources.Add(flightCloudMaterial);
        var renderer=go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial=flightCloudMaterial;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        for(int i=0;i<count;i++)
        {
            flightCloudSeeds[i]=new Vector3((float)random.NextDouble()*100-50,(float)random.NextDouble()*100,12+(float)random.NextDouble()*40);
            flightCloudParticles[i].startSize3D=new Vector3(16+(float)random.NextDouble()*24,3+(float)random.NextDouble()*5,1);
            flightCloudParticles[i].startLifetime=1000;
            flightCloudParticles[i].remainingLifetime=1000;
        }
    }

    void UpdateFlightClouds()
    {
        if(flightClouds==null) return;
        float density=SmoothRange(300,1800,Altitude)*(1-SmoothRange(11000,18000,Altitude));
        bool visible=!WelcomeVisible && Simulation.Resultado==null && density>.001f;
        flightClouds.gameObject.SetActive(visible);
        if(!visible) return;
        flightCloudMaterial.SetFloat("_EffectTime",flightPresentationTime*.15f);
        flightClouds.transform.position=rocket.position;
        Vector3 flow=Simulation.Fase==Fase.Despegue?Vector3.down:-VisualFlightDirection;
        Vector3 side=new Vector3(flow.y,-flow.x,0);
        for(int i=0;i<flightCloudParticles.Length;i++)
        {
            Vector3 seed=flightCloudSeeds[i];
            float travel=Mathf.Repeat(seed.y+cloudTravel,100)-50;
            flightCloudParticles[i].position=side*seed.x+flow*travel+Vector3.forward*seed.z;
            float edge=1-SmoothRange(28,50,Mathf.Abs(travel));
            flightCloudParticles[i].startColor=new Color(.76f,.86f,.91f,density*edge*.22f);
        }
        flightClouds.SetParticles(flightCloudParticles,flightCloudParticles.Length);
    }

    void ResetFlightAtmosphere()
    {
        flightPresentationTime=cloudTravel=0;
        if(flightClouds!=null) { flightClouds.Clear(); flightClouds.gameObject.SetActive(false); }
        if(launchCamera!=null) launchCamera.fieldOfView=36;
    }
}
