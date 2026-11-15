using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Artemis;

public static class VerifyRadioRecovery
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static LaunchDemo View=>UnityEngine.Object.FindAnyObjectByType<LaunchDemo>();
    static T Field<T>(string name)=>(T)typeof(LaunchDemo).GetField(name,Private).GetValue(View);
    static T RadioField<T>(string name)=>(T)typeof(MissionRadio).GetField(name,Private).GetValue(View.Radio);
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Debug.Log("[Radio/recovery] PASS: "+message); }
    static UnityEngine.UI.Button Button(string name)=>View.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name);
    static void Clickable(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases(); var rect=button.GetComponent<RectTransform>();
        Vector2 p=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var hits=new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);
        Check(button.IsInteractable() && p.x>=0 && p.x<=Screen.width && p.y>=0 && p.y<=Screen.height
            && hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Button receives pointer: "+button.name);
    }
    public static async Task<object> Falling()
    {
        if(View.PresentationPaused) View.TogglePause();
        var canopies=Field<Transform[]>("recoveryCanopies");
        await Task.Delay(450); float high=canopies[1].position.y;
        await Task.Delay(1250); float low=canopies[1].position.y;
        Check(low<high-.35f,"Released parachutes visibly fall instead of freezing");
        Check(Field<GameObject>("parachutes").transform.parent!=Field<Transform>("crewCabin"),"Spent canopies detach from the floating capsule");
        await Task.Delay(2900);
        Check(Field<GameObject>("parachutes").activeSelf,"Wet fabric remains visible after collapse");
        float sea=Field<GameObject>("ocean").transform.position.y;
        Check(canopies.All(c=>Mathf.Abs(c.position.y-sea)<.3f),"Collapsed canopies settle onto the animated water surface");
        View.TogglePause(); await Task.Delay(80);
        Vector3 p=canopies[1].position; await Task.Delay(200);
        Check(Vector3.Distance(p,canopies[1].position)<.0001f,"Pause freezes the falling and sinking rig");
        return new{age=Field<float>("splashdownAge"),drop=high-low,height=canopies[1].position.y-sea};
    }
    public static async Task<object> Sinking()
    {
        if(View.PresentationPaused) View.TogglePause();
        await Task.Delay(7500);
        float sea=Field<GameObject>("ocean").transform.position.y;
        Check(Field<Transform[]>("recoveryCanopies").All(c=>c.position.y<sea-.65f),"All three canopies sink below the water before culling");
        Check(!Field<GameObject>("parachutes").activeSelf,"No abrupt disappearance above the surface");
        return new{age=Field<float>("splashdownAge")};
    }
    public static async Task<string> Radio()
    {
        if(View.PresentationPaused) View.TogglePause();
        await Task.Delay(250);
        var radio=View.Radio; var m=View.Simulation;
        Check(radio!=null && radio.SignalPercent>0 && !radio.Blackout,"Tierra–Orion link is available after splashdown");
        Check(radio.Transcript.Contains("Amerizaje confirmado"),"Touchdown produces a ground-control radio message");
        double time=m.Tiempo,mass=m.Masa,rcs=m.Capsula.ReservaRcs;
        Clickable(Button("RadioAcknowledge")); Button("RadioAcknowledge").onClick.Invoke();
        await Task.Delay(65);
        Check(radio.LatestMessage.Contains("Cuatro astronautas a salvo") && !radio.AwaitingAcknowledgement,"Crew acknowledges safe recovery");
        Check(RadioField<AudioSource>("radioAudio").isPlaying,"Acknowledgement plays its radio sound");
        View.ToggleAudio(); await Task.Delay(60);
        Check(RadioField<AudioSource>("radioAudio").mute && RadioField<AudioSource>("interferenceAudio").mute,"Global audio control mutes both radio sources");
        View.ToggleAudio();
        Clickable(Button("RadioStatus")); Button("RadioStatus").onClick.Invoke();
        Check(!radio.RequestStatus(),"Status request cooldown blocks repeated transmissions");
        await Task.Delay(4000);
        Check(radio.LatestMessage.Contains("Baliza localizada"),"Ground control responds to the status request");
        Check(m.Tiempo==time && m.Masa==mass && m.Capsula.ReservaRcs==rcs,"Radio commands do not modify mission physics");
        Clickable(Button("OpenRadio")); Button("OpenRadio").onClick.Invoke(); await Task.Delay(130);
        Clickable(Button("CloseRadio")); Clickable(Button("ConsoleAcknowledge"));
        Check(View.GetComponent<LaunchHud>().BlocksFlightShortcuts,"Radio console blocks flight shortcuts");
        View.GetComponent<LaunchHud>().HandleEscape(); Check(!Button("CloseRadio").gameObject.activeInHierarchy,"Escape closes the radio console");
        View.ShowWelcome(); await Task.Delay(80); int revision=radio.Revision;
        await Task.Delay(220);
        Check(radio.Revision==revision && !radio.CanTransmit && !RadioField<AudioSource>("radioAudio").isPlaying,"Menu suspends radio delivery and sound");
        View.BeginMission(); await Task.Delay(80);
        return "Radio messages, acknowledgements, reply, audio, pointer controls and menu passed";
    }
    public static async Task<string> Blackout()
    {
        if(View.PresentationPaused) View.TogglePause();
        await Task.Delay(120);
        var radio=View.Radio;
        Check(View.Simulation.Fase==Fase.Regreso && View.Simulation.Calor>650000,"Fixture is in hot atmospheric reentry");
        Check(radio.Blackout && radio.SignalPercent==0,"Reentry plasma interrupts the radio link");
        Check(!radio.Acknowledge() && !radio.RequestStatus(),"No transmission is delivered during blackout");
        Check(RadioField<AudioSource>("interferenceAudio").isPlaying,"Blackout emits quiet interference");
        View.TogglePause(); await Task.Delay(80);
        Check(!RadioField<AudioSource>("interferenceAudio").isPlaying,"Pause suspends radio interference");
        return "Blackout and audio pause verified";
    }
    public static async Task<string> Reset()
    {
        View.ResetDemo(); View.ShowWelcome(); await Task.Delay(150);
        Check(!View.Radio.Transcript.Contains("Amerizaje confirmado") && !View.Radio.Blackout,"Restart clears old radio messages and blackout");
        Check(!RadioField<AudioSource>("radioAudio").isPlaying && !RadioField<AudioSource>("interferenceAudio").isPlaying,"Restart does not leave stale radio sound playing");
        Check(View.GetComponentsInChildren<MissionRadio>(true).Length==1,"Restart reuses one communication module");
        return "Radio reset passed";
    }
}
