using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Artemis;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Run in Play Mode through Pipeline. Deterministic fixtures never ship in the player.
public static class VerifyReturnFlight
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class NoFailures : IRandom { public double Next()=>.99999; }
    static LaunchDemo View=>UnityEngine.Object.FindAnyObjectByType<LaunchDemo>();
    static T Field<T>(string name)=>(T)typeof(LaunchDemo).GetField(name,Private).GetValue(View);
    static void Pose()=>typeof(LaunchDemo).GetMethod("UpdateVehiclePose",Private).Invoke(View,null);
    static void Check(bool value,string message) { if(!value) throw new Exception(message); Debug.Log("[ReturnFlight check] PASS: "+message); }
    static void FlightState(LaunchDemo.FlightState state)=>typeof(LaunchDemo).GetProperty("State").SetValue(View,state);

    static void CheckVisibleControls()
    {
        Canvas.ForceUpdateCanvases();
        var hits=new System.Collections.Generic.List<RaycastResult>();
        int count=0;
        foreach(var control in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Selectable>())
        {
            if(!control.IsInteractable()) continue;
            var rect=control.GetComponent<RectTransform>();
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            if(point.x<0 || point.x>Screen.width || point.y<0 || point.y>Screen.height)
                throw new Exception("Control outside viewport: "+control.name);
            var pointer=new PointerEventData(EventSystem.current) { position=point };
            hits.Clear(); EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>()!=control)
                throw new Exception("Control blocked by another graphic: "+control.name);
            count++;
        }
        Check(count>0,"All "+count+" active controls are on screen and receive pointer raycasts");
    }

    static Mision Fixture()
    {
        View.BeginMission(); View.ResetDemo();
        var data=MisionDemoRunner.CargarDatos();
        var parameters=new GeneradorParametros(data).Generar(2026);
        parameters.Presupuesto=3876; parameters.Clima="Soleado"; parameters.Latitud=parameters.Inclinacion=28.627;
        var mission=new Mision(data,parameters,new NoFailures());
        typeof(LaunchDemo).GetProperty("Simulation").SetValue(View,mission);
        return mission;
    }

    public static async Task<string> Controls()
    {
        var v=View; var m=Fixture();
        await Task.Delay(150);
        CheckVisibleControls();
        m.Abordar(); v.Launch();
        Check(v.State==LaunchDemo.FlightState.Countdown,"Launch button starts countdown");
        v.TogglePause(); float remaining=v.Countdown;
        await Task.Delay(350);
        Check(Mathf.Abs(v.Countdown-remaining)<.001f,"Pause freezes countdown");
        v.TogglePause();
        var input=UnityEngine.Object.FindAnyObjectByType<TMP_InputField>();
        EventSystem.current.SetSelectedGameObject(input.gameObject);
        remaining=v.Countdown;
        await Task.Delay(350);
        Check(v.Countdown<remaining,"Selected seed field does not freeze countdown");
        v.ShowWelcome(); remaining=v.Countdown;
        await Task.Delay(150);
        CheckVisibleControls();
        await Task.Delay(350);
        Check(Mathf.Abs(v.Countdown-remaining)<.001f,"Welcome menu suspends countdown");
        v.BeginMission();
        await Task.Delay(350);
        Check(v.Countdown<remaining,"Continue resumes the same countdown");
        v.ResetDemo();
        await Task.Delay(100);
        Check(v.Simulation.Tiempo==0 && v.SpeedMultiplier==1 && Field<Mesh>("celestialSphere")!=null,"Restart resets flight and preserves planet mesh");
        v.ShowWelcome();
        return "6 interaction regressions passed";
    }

    public static async Task<string> Reentry()
    {
        var m=Fixture(); m.Abordar(); Check(m.Lanzar(),"Deterministic mission launches");
        FlightState(LaunchDemo.FlightState.Flying);
        for(int i=0;i<100000 && !m.ListaReentrada && m.Resultado==null;i++)
        {
            if(m.ListaTli) m.InyectarTli();
            m.Avanzar(m.Paso);
        }
        Check(m.ListaReentrada,"Mission reaches Earth return decision");
        m.SepararServicio();
        await Task.Delay(150);
        var angle=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Slider>().Single(s=>s.name=="EntryAngle");
        angle.value=-5.5f;
        await Task.Delay(600);
        Check(Mathf.Abs(View.VisualFlightAngle+5.5f)<.001f,"Cabin previews chosen shallow entry angle");
        angle.value=-6.5f;
        await Task.Delay(600);
        Check(Mathf.Abs(View.VisualFlightAngle+6.5f)<.001f,"Cabin previews chosen steep entry angle");
        m.AjustarEntrada(-6);
        m.Datos.Juego["velocidad_tiempo_reingreso"]=1;
        Check(m.Reingresar(),"Service separation enables reentry");
        await Task.Delay(800);
        Pose(); Vector3 previous=Field<Transform>("rocket").position;
        m.Avanzar(1);
        // UpdateVehiclePose normally follows AdvanceMission, which writes compressed height.
        typeof(LaunchDemo).GetMethod("AdvanceMission",Private).Invoke(View,new object[]{0f});
        Pose(); Vector3 delta=Field<Transform>("rocket").position-previous;
        Check(delta.x>0 && delta.y<0,"Capsule travels horizontally while descending");
        float measured=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
        Check(Mathf.Abs(measured-View.VisualFlightAngle)<.1f,"Visible path follows physical flight angle");
        await Task.Delay(800);
        Check(Vector3.Dot(-Field<Transform>("crewCabin").up,View.VisualFlightDirection)>.98f,"Heat shield faces direction of flight");
        m.Pausar();
        Vector3 position=Field<Transform>("rocket").position;
        await Task.Delay(350);
        Check(Vector3.Distance(position,Field<Transform>("rocket").position)<.001f,"Pause freezes capsule trajectory");
        return "Reentry and angle checks passed; paused for screenshot";
    }

    public static async Task<string> Landing()
    {
        var m=View.Simulation;
        if(m.Pausada) m.Pausar();
        for(int i=0;i<100000 && m.Resultado==null && !m.Principales;i++) m.Avanzar(m.Paso);
        Check(m.Principales,"Main parachutes deploy during descent");
        await Task.Delay(1500);
        Check(Vector3.Dot(Field<Transform>("crewCabin").up,Vector3.up)>.99f,"Cabin hangs upright under parachutes");
        Check(Quaternion.Angle(Field<GameObject>("parachutes").transform.rotation,Quaternion.identity)<.1f,"Canopies stay above cabin");
        m.Pausar();
        return "Landing checks passed; paused for screenshot";
    }

    public static async Task<string> Finish()
    {
        var m=View.Simulation; if(m.Pausada) m.Pausar();
        for(int i=0;i<100000 && m.Resultado==null;i++) m.Avanzar(m.Paso);
        Check(m.Resultado?.Tipo==TipoResultado.Exito,"Full mission ends with safe splashdown");
        await Task.Delay(150);
        Check(Mathf.Abs(Field<Transform>("crewCabin").position.y-.33f-Field<GameObject>("ocean").transform.position.y)<.05f,"Capsule reaches ocean surface");
        return "Full return flow passed";
    }
}

