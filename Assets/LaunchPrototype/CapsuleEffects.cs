using Artemis;
using UnityEngine;
using UnityEngine.Rendering;

// Presentation only: reads telemetry, never applies forces or consumes propellant.
public sealed partial class LaunchDemo
{
    ParticleSystem[] entryPlasma;
    ParticleSystem[][] capsuleJets;
    Light capsuleFireLight;
    Material capsuleFireMaterial;
    float effectTime, heatBlend, maneuverPulse;
    double previousRcs, previousBank;

    void BuildCapsuleEffects()
    {
        heatBlend=maneuverPulse=0;
        previousRcs=Simulation.Capsula.ReservaRcs;
        previousBank=Simulation.Alabeo;
        if(capsuleFireMaterial==null)
        {
            capsuleFireMaterial=new Material(Resources.Load<Shader>("AuroraExhaust"));
            capsuleFireMaterial.SetFloat("_Intensity",1.45f);
            capsuleFireMaterial.SetFloat("_UseEffectTime",1);
            generatedResources.Add(capsuleFireMaterial);
        }
        entryPlasma=new[]
        {
            CapsuleFlame("Plasma / shield rim",new Vector3(0,-.34f,0),new Color(1,.65f,.2f,.55f),new Color(1,.14f,.015f,0),.10f,.19f,.07f,.14f,180,1.7f,.30f,10),
            CapsuleFlame("Plasma / turbulent sheath",new Vector3(0,-.31f,0),new Color(1,.33f,.055f,.40f),new Color(.8f,.035f,.008f,0),.13f,.25f,.25f,.48f,145,3.0f,.32f,16),
            CapsuleFlame("Plasma / trailing wisps",new Vector3(0,-.28f,0),new Color(1,.45f,.12f,.27f),new Color(.65f,.06f,.01f,0),.10f,.20f,.5f,.85f,90,5.8f,.36f,11)
        };
        capsuleJets=new ParticleSystem[4][];
        for(int i=0;i<capsuleJets.Length;i++)
        {
            float a=(45+i*90)*Mathf.Deg2Rad;
            Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Vector3 direction=(radial*.75f+Vector3.down).normalized;
            var socket=new GameObject("Orion RCS nozzle "+(i+1)).transform;
            socket.SetParent(crewCabin,false);
            socket.localPosition=radial*.27f+Vector3.down*.13f;
            socket.localRotation=Quaternion.FromToRotation(Vector3.down,direction);
            var nozzle=Part(socket,"Thruster bell",PrimitiveType.Cylinder,Vector3.down*.03f,new Vector3(.065f,.04f,.065f),new Color(.13f,.18f,.22f));
            // Decorative nozzles must not add collision surfaces to the simulation.
            Destroy(nozzle.GetComponent<Collider>());
            capsuleJets[i]=new[]
            {
                CreatePlumeLayer(socket,"RCS / blue core",Vector3.down*.075f,1,new Color(.48f,.79f,1,.85f),new Color(.15f,.32f,1,0),.035f,.055f,.055f,.10f,90,5,3.2f),
                CreatePlumeLayer(socket,"RCS / amber plume",Vector3.down*.075f,1,new Color(1,.69f,.25f,.55f),new Color(1,.15f,.025f,0),.055f,.095f,.12f,.22f,75,9,3.0f)
            };
            foreach(var ps in capsuleJets[i])
            {
                var shape=ps.shape; shape.radius=.018f;
                ConfigureCapsuleParticle(ps,.02f);
                StretchFlame(ps,1.4f,.04f);
            }
        }
        capsuleFireLight=new GameObject("Orion plasma light").AddComponent<Light>();
        capsuleFireLight.transform.SetParent(crewCabin,false);
        capsuleFireLight.transform.localPosition=new Vector3(0,-.5f,-.25f);
        capsuleFireLight.type=LightType.Point;
        capsuleFireLight.range=3;
        capsuleFireLight.color=new Color(1,.28f,.055f);
        capsuleFireLight.intensity=0;
        capsuleFireLight.shadows=LightShadows.None;
    }

    ParticleSystem CapsuleFlame(string name,Vector3 position,Color start,Color end,float minSize,float maxSize,float minLife,float maxLife,float rate,float speed,float radius,float angle)
    {
        var ps=CreatePlumeLayer(crewCabin,name,position,1,start,end,minSize,maxSize,minLife,maxLife,rate,angle,speed);
        var shape=ps.shape;
        shape.rotation=new Vector3(-90,0,0); // From the shield rim, along the capsule's wake.
        shape.radius=radius;
        shape.radiusThickness=.12f;
        ConfigureCapsuleParticle(ps,.04f);
        if(speed>2) { shape.angle=8; StretchFlame(ps,1.7f,.08f); }
        return ps;
    }

    static void StretchFlame(ParticleSystem ps,float length,float velocity)
    {
        var renderer=ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Stretch;
        renderer.lengthScale=length;
        renderer.velocityScale=velocity;
    }

    void ConfigureCapsuleParticle(ParticleSystem ps,float turbulence)
    {
        var main=ps.main;
        main.maxParticles=180;
        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var noise=ps.noise;
        noise.enabled=true;
        noise.strength=turbulence;
        noise.frequency=3.5f;
        noise.scrollSpeed=1.3f;
        noise.quality=ParticleSystemNoiseQuality.Low;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=capsuleFireMaterial;
    }

    static void DriveFlame(ParticleSystem ps,float power,float rate,bool frozen,bool finished)
    {
        if(ps==null) return;
        if(finished)
        {
            if(ps.isPlaying || ps.particleCount>0) ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }
        var main=ps.main; main.simulationSpeed=frozen?0:1;
        var emission=ps.emission;
        emission.rateOverTime=rate*power;
        emission.enabled=power>.01f;
        if(power>.01f && !ps.isPlaying) ps.Play(false);
    }

    void UpdateCapsuleEffects()
    {
        if(entryPlasma==null || capsuleJets==null) return;
        var m=Simulation;
        bool finished=m.Resultado!=null;
        bool frozen=WelcomeVisible || m.Pausada || finished || m.DecisionMotor || m.ListaTli || m.ListaReentrada;
        if(!frozen) effectTime+=Time.deltaTime;
        capsuleFireMaterial.SetFloat("_EffectTime",effectTime);
        if(fireMaterial!=null)
        {
            fireMaterial.SetFloat("_UseEffectTime",1);
            fireMaterial.SetFloat("_EffectTime",effectTime);
        }
        float target=m.Fase==Fase.Regreso && !finished
            ? Mathf.InverseLerp(80000,1400000,(float)m.Calor)*Mathf.InverseLerp(500,3000,(float)m.Velocidad) : 0;
        if(!frozen) heatBlend=Mathf.MoveTowards(heatBlend,target,Time.deltaTime*2);
        if(finished || m.Fase==Fase.Aterrizaje) heatBlend=0;
        float flicker=.85f+Mathf.PerlinNoise(effectTime*11,2)*.3f;
        for(int i=0;i<entryPlasma.Length;i++)
            DriveFlame(entryPlasma[i],heatBlend*flicker,i==0?180:i==1?145:70,frozen,finished);

        if(m.Capsula.ReservaRcs<previousRcs || Mathf.Abs((float)(m.Alabeo-previousBank))>2) maneuverPulse=.65f;
        previousRcs=m.Capsula.ReservaRcs; previousBank=m.Alabeo;
        if(!frozen) maneuverPulse=Mathf.Max(0,maneuverPulse-Time.deltaTime);
        bool outbound=m.Fase==Fase.Recorrido && !m.ListaReentrada || m.Fase==Fase.Despegue && m.LasSeparado;
        bool attitude=outbound || m.Fase==Fase.Regreso && heatBlend<.45f;
        float jetLight=0;
        for(int i=0;i<capsuleJets.Length;i++)
        {
            // Short alternating stabilization bursts; the domain owns all actual RCS usage.
            float cycle=Mathf.Repeat(effectTime+i*.83f,3.3f);
            float pulse=outbound && cycle<.42f?Mathf.Sin(cycle/.42f*Mathf.PI):0;
            float power=attitude && m.Capsula.ReservaRcs>0 && !m.FalloElectrico && !finished
                ? Mathf.Max(pulse,maneuverPulse*(i%2==0?1:0)) : 0;
            for(int layer=0;layer<capsuleJets[i].Length;layer++)
                DriveFlame(capsuleJets[i][layer],power,layer==0?90:75,frozen,finished);
            jetLight=Mathf.Max(jetLight,power);
        }
        capsuleFireLight.intensity=finished?0:heatBlend*flicker*2.2f+jetLight*.25f;
    }
}
