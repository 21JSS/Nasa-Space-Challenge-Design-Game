using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Artemis;
using UnityEngine;
using UnityEngine.EventSystems;

public static class VerifyGameModes
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static LaunchDemo View=>UnityEngine.Object.FindAnyObjectByType<LaunchDemo>();
    static T Field<T>(string name)=>(T)typeof(LaunchDemo).GetField(name,Private).GetValue(View);
    static UnityEngine.UI.Button Button(string name)=>View.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name);
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Debug.Log("[Modes/FX check] PASS: "+message); }
    static void CheckControls()
    {
        Canvas.ForceUpdateCanvases();
        var hits=new System.Collections.Generic.List<RaycastResult>();
        foreach(var control in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Selectable>())
        {
            if(!control.IsInteractable()) continue;
            var rect=control.GetComponent<RectTransform>();
            Vector2 p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Check(p.x>=0 && p.x<=Screen.width && p.y>=0 && p.y<=Screen.height,"Visible: "+control.name);
            hits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);
            Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>()==control,"Receives clicks: "+control.name);
        }
    }
    public static async Task<string> Controls()
    {
        View.ResetDemo(); View.ShowWelcome(); await Task.Delay(150); CheckControls();
        Button("CasualMode").onClick.Invoke();
        Check(View.Simulation.EsCasual,"Welcome selects casual");
        View.ResetDemo(); Check(View.Simulation.EsCasual,"Retry keeps mode");
        View.GenerateMission(2027,1); Check(View.Simulation.EsCasual,"New seed keeps mode");
        Button("RealisticMode").onClick.Invoke();
        Check(!View.Simulation.EsCasual,"Welcome selects realistic");
        View.BeginMission(); await Task.Delay(100); Button("GameMode").onClick.Invoke();
        Check(View.Simulation.EsCasual,"Design screen changes mode"); CheckControls();
        View.Simulation.EsperarClima(); View.Simulation.Abordar(); View.Launch();
        Check(View.State==LaunchDemo.FlightState.Countdown,"Countdown starts");
        View.ChooseGameMode(ModoJuego.Realista); Check(View.Simulation.EsCasual,"Countdown locks mode");
        View.ResetDemo(); View.ShowWelcome();
        return "Mode controls, pointer routing, persistence and countdown lock passed";
    }
    static Mision Fixture()
    {
        View.ResetDemo(); View.BeginMission();
        var d=MisionDemoRunner.CargarDatos();
        var p=new GeneradorParametros(d).Generar(2026);
        p.Presupuesto=3876; p.Clima="Soleado"; p.Latitud=p.Inclinacion=28.627;
        var m=new Mision(d,p,new MisionDemoRunner.SinFallos());
        typeof(LaunchDemo).GetProperty("Simulation").SetValue(View,m);
        m.OnEvento+=e=>Debug.Log("[Modes/FX flight] "+e);
        if(!View.CloseCamera) View.ToggleCamera();
        m.Abordar(); m.Lanzar();
        typeof(LaunchDemo).GetProperty("State").SetValue(View,LaunchDemo.FlightState.Flying);
        return m;
    }
    public static async Task<object> Jets()
    {
        var m=Fixture();
        while(!m.ListaTli && m.Resultado==null) m.Avanzar(.2);
        Check(m.InyectarTli(),"TLI starts outbound coast");
        m.Datos.Juego["velocidad_tiempo_crucero"]=1;
        m.Avanzar(12240);
        await Task.Delay(1800);
        var jets=Field<ParticleSystem[][]>("capsuleJets");
        int count=jets.Sum(pair=>pair.Sum(p=>p.particleCount));
        for(int i=0;i<20 && count==0;i++) { await Task.Delay(70); count=jets.Sum(pair=>pair.Sum(p=>p.particleCount)); }
        Check(count>0,"Outbound capsule emits animated RCS bursts");
        m.Pausar(); await Task.Delay(120);
        Check(jets.SelectMany(p=>p).All(p=>p.main.simulationSpeed==0),"Pause freezes all capsule jets");
        return new{phase=m.Fase.ToString(),particles=count,rcs=m.Capsula.ReservaRcs};
    }
    public static async Task<object> Reentry()
    {
        var m=Fixture();
        while(!m.ListaReentrada && m.Resultado==null)
        { if(m.ListaTli) m.InyectarTli(); m.Avanzar(m.Paso); }
        Check(m.ListaReentrada,"Flight reaches return");
        m.SepararServicio(); m.AjustarEntrada(-5.8); Check(m.Reingresar(),"Entry starts");
        m.Datos.Juego["velocidad_tiempo_reingreso"]=1;
        while(m.Calor<900000 && m.Resultado==null && m.Fase==Fase.Regreso) m.Avanzar(.2);
        await Task.Delay(900);
        var particles=Field<ParticleSystem[]>("entryPlasma");
        Check(particles.Sum(p=>p.particleCount)>0,"Heat telemetry drives plasma particles");
        m.Pausar(); await Task.Delay(100);
        double t=m.Tiempo,mass=m.Masa,rcs=m.Capsula.ReservaRcs,heat=m.Calor;
        var state=(double[])m.EstadoReingreso.Clone();
        float animationTime=Field<float>("effectTime");
        await Task.Delay(400);
        Check(Field<float>("effectTime")==animationTime && particles.All(p=>p.main.simulationSpeed==0),"Pause freezes plasma and shader animation");
        Check(m.Tiempo==t && m.Masa==mass && m.Capsula.ReservaRcs==rcs && m.Calor==heat && state.SequenceEqual(m.EstadoReingreso),"FX does not mutate flight state or fuel");
        m.Pausar(); View.ShowWelcome(); await Task.Delay(100);
        double menuTime=m.Tiempo; float menuAnimation=Field<float>("effectTime");
        await Task.Delay(250);
        Check(m.Tiempo==menuTime && Field<float>("effectTime")==menuAnimation,"Menu suspends flight and animation");
        View.BeginMission(); m.Pausar();
        return new{height=m.Altura,speed=m.Velocidad,heat=m.Calor,particles=particles.Sum(p=>p.particleCount)};
    }
    public static async Task<string> Landing()
    {
        var m=View.Simulation; if(m.Pausada) m.Pausar();
        while(m.Resultado==null && !m.Principales) m.Avanzar(.2);
        Check(m.Principales,"Recovery parachutes deploy");
        m.Datos.Juego["velocidad_tiempo_descenso"]=1;
        if(View.CloseCamera) View.ToggleCamera();
        await Task.Delay(1600);
        Check(Field<ParticleSystem[]>("entryPlasma").All(p=>p.particleCount==0),"Plasma clears during parachute descent");
        m.Pausar();
        return "Recovery descent preserves parachutes; no residual plasma";
    }
    public static async Task<string> Finish()
    {
        var m=View.Simulation; if(m.Pausada) m.Pausar();
        for(int i=0;i<15000 && m.Resultado==null;i++) m.Avanzar(.2);
        Check(m.Resultado?.Tipo==TipoResultado.Exito,"Visual fixture reaches safe splashdown");
        await Task.Delay(150);
        Check(Field<ParticleSystem[]>("entryPlasma").All(p=>p.particleCount==0),"Finished mission clears effects");
        View.ResetDemo(); View.ChooseGameMode(ModoJuego.Realista); View.GenerateMission(2026,1); View.ShowWelcome();
        await Task.Delay(150);
        Check(Field<ParticleSystem[]>("entryPlasma").All(p=>p.particleCount==0),"Restart has no stale flames");
        return "Splashdown and restart verified; welcome ready";
    }
}
