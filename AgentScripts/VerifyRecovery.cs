using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Artemis;
using UnityEngine;
using UnityEditor;

// Ephemeral Play Mode checks. No debug component is included in the player.
public static class VerifyRecovery
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static LaunchDemo View=>UnityEngine.Object.FindAnyObjectByType<LaunchDemo>();
    static T Field<T>(string name)=>(T)typeof(LaunchDemo).GetField(name,Private).GetValue(View);
    static void Check(bool value,string message) { if(!value) throw new Exception(message); Debug.Log("[Recovery check] PASS: "+message); }
    static void Invoke(string name,params object[] args)=>typeof(LaunchDemo).GetMethod(name,Private).Invoke(View,args);
    public static async Task<object> Descent()
    {
        View.ResetDemo(); View.BeginMission();
        if(View.CloseCamera) View.ToggleCamera();
        var d=MisionDemoRunner.CargarDatos();
        var p=new GeneradorParametros(d).Generar(2026);
        p.Presupuesto=3876; p.Clima="Soleado"; p.Latitud=p.Inclinacion=28.627;
        var m=new Mision(d,p,new MisionDemoRunner.SinFallos());
        typeof(LaunchDemo).GetProperty("Simulation").SetValue(View,m);
        m.Abordar(); Check(m.Lanzar(),"Deterministic mission launches");
        typeof(LaunchDemo).GetProperty("State").SetValue(View,LaunchDemo.FlightState.Flying);
        for(int i=0;i<100000 && !m.ListaReentrada && m.Resultado==null;i++)
        { if(m.ListaTli) m.InyectarTli(); m.Avanzar(m.Paso); }
        Check(m.ListaReentrada,"Complete flight reaches return");
        m.SepararServicio(); m.AjustarEntrada(-5.8); Check(m.Reingresar(),"Reentry begins");
        for(int i=0;i<100000 && !m.Principales && m.Resultado==null;i++) m.Avanzar(m.Paso);
        Check(m.Principales && m.Resultado==null,"Main canopies deploy in flight");
        await Task.Delay(2600);
        Check(Field<GameObject>("ocean").activeInHierarchy,"Ocean is visible during descent");
        Check(Field<GameObject>("recoverySky").activeInHierarchy,"Textured sky replaces empty backdrop");
        Check(!Field<Transform>("launchGround").gameObject.activeSelf,"Launch structures stay out of the ocean environment");
        Check(Field<GameObject>("parachutes").transform.localScale.x>.9f,"Canopies finish their progressive inflation");
        Check(View.EffectiveTimeRate<=6.01f,"Normal descent is extended from the previous 12x rate");
        foreach(string name in new[]{"AuroraOcean","AuroraRecoverySky"})
        {
            var shader=Resources.Load<Shader>(name);
            Check(shader!=null && shader.isSupported && !ShaderUtil.ShaderHasError(shader),name+" compiles on the active graphics device");
        }
        m.Pausar(); await Task.Delay(100);
        float clock=Field<float>("recoveryTime"); var cabin=Field<Transform>("crewCabin");
        Vector3 position=cabin.position; Quaternion rotation=cabin.rotation;
        double missionTime=m.Tiempo, mass=m.Masa;
        await Task.Delay(300);
        Check(clock==Field<float>("recoveryTime") && Vector3.Distance(position,cabin.position)<.0001f && Quaternion.Angle(rotation,cabin.rotation)<.01f,"Pause freezes water, trajectory and canopy sway");
        Check(m.Tiempo==missionTime && m.Masa==mass,"Presentation preserves paused physical telemetry");
        return new{altitude=m.Altura,rate=View.EffectiveTimeRate,frame=Time.frameCount};
    }
    public static async Task<object> Approach()
    {
        var m=View.Simulation; if(m.Pausada) m.Pausar();
        for(int i=0;i<100000 && m.Altura>45 && m.Resultado==null;i++) m.Avanzar(m.Paso);
        await Task.Delay(200);
        Check(m.Resultado==null && m.Altura>0,"Final approach remains above the ocean");
        Check(View.EffectiveTimeRate<1.4f,"Final approach slows close to real time");
        m.Pausar(); await Task.Delay(80);
        return new{altitude=m.Altura,rate=View.EffectiveTimeRate};
    }
    public static async Task<object> Touchdown()
    {
        var m=View.Simulation; if(m.Pausada) m.Pausar();
        for(int i=0;i<100000 && m.Resultado==null;i++) m.Avanzar(m.Paso);
        Check(m.Resultado?.Tipo==TipoResultado.Exito,"Physical mission finishes with a safe splashdown");
        await Task.Delay(120);
        Check(Field<ParticleSystem>("recoverySpray").particleCount>0,"Touchdown emits water spray");
        View.TogglePause();
        return new{particles=Field<ParticleSystem>("recoverySpray").particleCount,age=Field<float>("splashdownAge"),speed=m.Resultado.VelocidadAmerizaje};
    }
    public static async Task<object> Float()
    {
        if(Field<bool>("recoveryPaused")) View.TogglePause();
        await Task.Delay(13000);
        var m=View.Simulation; var cabin=Field<Transform>("crewCabin");
        double missionTime=m.Tiempo, height=m.Altura, speed=m.Velocidad;
        Vector3 before=cabin.position; Quaternion angle=cabin.rotation;
        float clock=Field<float>("recoveryTime");
        await Task.Delay(650);
        Check(Field<float>("recoveryTime")>clock && Vector3.Distance(before,cabin.position)>.001f,"Ocean and capsule keep moving after the result");
        Check(Quaternion.Angle(angle,cabin.rotation)>.05f,"Capsule rocks with the waves");
        Check(m.Tiempo==missionTime && m.Altura==height && m.Velocidad==speed,"Buoyancy does not alter mission result or telemetry");
        Check(!Field<GameObject>("parachutes").activeSelf,"Parachutes are culled after submerging below the water");
        float sea=Field<GameObject>("ocean").transform.position.y;
        Check(cabin.position.y-sea>.15f && cabin.position.y-sea<.4f,"Heat shield stays partly immersed while the cabin floats");
        View.ShowWelcome(); await Task.Delay(100);
        clock=Field<float>("recoveryTime"); await Task.Delay(250);
        Check(clock==Field<float>("recoveryTime") && !Field<GameObject>("ocean").activeSelf,"Menu suspends and hides the recovery environment");
        View.BeginMission(); await Task.Delay(150);
        int systemCount=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length;
        var spray=Field<ParticleSystem>("recoverySpray");
        for(int i=0;i<10;i++) { spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); spray.Play(); spray.Emit(140); }
        Check(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length==systemCount,"Ten splash bursts reuse one particle system");
        spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        View.TogglePause(); await Task.Delay(100);
        clock=Field<float>("recoveryTime"); before=cabin.position;
        await Task.Delay(250);
        Check(clock==Field<float>("recoveryTime") && before==cabin.position,"Pause also works after mission completion");
        return new{immersion=cabin.position.y-sea,age=Field<float>("splashdownAge"),missionSpeed=m.Velocidad};
    }
    public static async Task<string> Restart()
    {
        var mesh=Field<Mesh>("recoveryOceanMesh"); int particles=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length;
        View.ResetDemo(); await Task.Delay(150);
        Check(mesh!=null && Field<GameObject>("ocean").GetComponent<MeshFilter>().sharedMesh==mesh,"Restart preserves the reusable ocean mesh");
        Check(!Field<GameObject>("ocean").activeSelf && !Field<GameObject>("recoverySky").activeSelf,"New launch has no recovery backdrop");
        Check(Field<ParticleSystem>("recoverySpray").particleCount==0 && Field<float>("splashdownAge")==-1,"Restart clears the impact effect");
        Check(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length<=particles,"Restart does not duplicate particle systems");
        View.ShowWelcome(); return "Recovery reset passed";
    }
}
