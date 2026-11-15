using System.Collections.Generic;
using System.Text;
using Artemis;
using UnityEngine;

/// <summary>Local mission radio: scripted messages, acknowledgements and short radio effects.</summary>
public sealed class MissionRadio : MonoBehaviour
{
    struct Transmission { public string Text; public bool Acknowledge; }
    readonly Queue<Transmission> incoming=new Queue<Transmission>();
    readonly Queue<string> history=new Queue<string>();
    readonly StringBuilder transcript=new StringBuilder(1800);
    LaunchDemo view;
    Mision mission;
    AudioSource radioAudio, interferenceAudio;
    AudioClip receiveClip, transmitClip, interferenceClip;
    Fase previousPhase;
    bool previousDrogues,previousMains,previousCrew,previousCountdown,nearSeaAnnounced,resultAnnounced;
    bool audioSuspended;
    float clock,nextReception,requestAvailableAt;
    string pendingReply;
    float replyAt;

    public string LinkStatus { get; private set; }="EN ESPERA";
    public string LatestMessage { get; private set; }="Comprobación del enlace Tierra–Orión.";
    public string Transcript { get; private set; }="";
    public int SignalPercent { get; private set; }
    public int Revision { get; private set; }
    public bool Blackout { get; private set; }
    public bool AwaitingAcknowledgement { get; private set; }
    public bool CanTransmit => view!=null && mission!=null && !view.WelcomeVisible && !view.PresentationPaused && !Blackout && mission.Resultado?.Tipo!=TipoResultado.PerdidaTotal;
    public bool CanAcknowledge => CanTransmit && AwaitingAcknowledgement;
    public bool CanRequestStatus => CanTransmit && clock>=requestAvailableAt;

    public void Initialize(LaunchDemo controller)
    {
        view=controller;
        radioAudio=gameObject.AddComponent<AudioSource>(); radioAudio.playOnAwake=false; radioAudio.spatialBlend=0; radioAudio.volume=.22f;
        interferenceAudio=gameObject.AddComponent<AudioSource>(); interferenceAudio.playOnAwake=false; interferenceAudio.spatialBlend=0; interferenceAudio.volume=.065f; interferenceAudio.loop=true;
        receiveClip=CreateRadioClip("Radio / incoming squelch",.65f,1420,false);
        transmitClip=CreateRadioClip("Radio / acknowledgement",.38f,1080,false);
        interferenceClip=CreateRadioClip("Radio / plasma interference",1.8f,0,true);
        interferenceAudio.clip=interferenceClip;
    }

    void Update()
    {
        if(view?.Simulation==null || radioAudio==null) return;
        if(mission!=view.Simulation) BindMission(view.Simulation);
        bool suspended=view.WelcomeVisible || view.PresentationPaused;
        radioAudio.mute=interferenceAudio.mute=view.Muted;
        if(suspended!=audioSuspended)
        {
            audioSuspended=suspended;
            if(suspended) { radioAudio.Pause(); interferenceAudio.Pause(); }
            else { radioAudio.UnPause(); interferenceAudio.UnPause(); }
        }
        if(suspended) return;
        clock+=Mathf.Min(Time.deltaTime,.1f);
        bool blocked=mission.Resultado?.Tipo==TipoResultado.PerdidaTotal || mission.FalloElectrico
            || mission.Fase==Fase.Regreso && mission.Calor>650000 && mission.Altura>26000;
        if(blocked!=Blackout)
        {
            Blackout=blocked; Revision++;
            if(blocked)
            {
                AddMessage("CANAL",mission.FalloElectrico?"Enlace interrumpido. Activa el respaldo de energía.":"Interferencia de reentrada. Esperando recuperar señal.");
                if(!interferenceAudio.isPlaying) interferenceAudio.Play();
            }
            else
            {
                interferenceAudio.Stop();
                QueueGround("Orión, hemos recuperado la señal. Confirmen recepción.",true);
            }
        }
        SignalPercent=blocked?0:mission.Fase==Fase.Recorrido?86:mission.Fase==Fase.Regreso?62:98;
        LinkStatus=blocked?"SIN ENLACE":mission.Resultado?.Tipo==TipoResultado.Exito?"BALIZA DE RESCATE":"ENLACE ESTABLE";
        ObserveMission();
        if(!Blackout && pendingReply!=null && clock>=replyAt)
        {
            QueueGround(pendingReply,true); pendingReply=null;
        }
        if(!Blackout && incoming.Count>0 && clock>=nextReception)
        {
            var message=incoming.Dequeue();
            AddMessage("TIERRA",message.Text); AwaitingAcknowledgement=message.Acknowledge;
            radioAudio.PlayOneShot(receiveClip);
            nextReception=clock+3.5f;
        }
    }

    void BindMission(Mision next)
    {
        mission=next; incoming.Clear(); history.Clear(); Transcript=""; pendingReply=null;
        clock=nextReception=requestAvailableAt=0; Blackout=AwaitingAcknowledgement=false;
        previousPhase=next.Fase; previousDrogues=next.Drogues; previousMains=next.Principales;
        previousCrew=next.Abordados; previousCountdown=false; nearSeaAnnounced=resultAnnounced=false;
        radioAudio.Stop(); interferenceAudio.Stop(); audioSuspended=false;
        LatestMessage="Comprobación del enlace Tierra–Orión."; LinkStatus="EN ESPERA"; SignalPercent=0; Revision++;
        QueueGround(PhaseMessage(next.Fase),true);
    }

    void ObserveMission()
    {
        if(previousPhase!=mission.Fase)
        {
            previousPhase=mission.Fase;
            incoming.Clear(); // Outdated launch/coast calls must not play during recovery.
            QueueGround(PhaseMessage(mission.Fase),true);
        }
        if(mission.Abordados && !previousCrew) { previousCrew=true; QueueGround("Cuatro astronautas a bordo. Orión, confirmen lista de cabina.",true); }
        bool countdown=view.State==LaunchDemo.FlightState.Countdown;
        if(countdown && !previousCountdown) QueueGround("Secuencia de lanzamiento iniciada. Mantengan el canal libre.",false);
        previousCountdown=countdown;
        if(mission.Drogues && !previousDrogues) { previousDrogues=true; QueueGround("Frenado confirmado. Vigilen la apertura de los principales.",true); }
        if(mission.Principales && !previousMains) { previousMains=true; QueueGround("Tres principales abiertos. Descenso estabilizado hacia el Pacífico.",true); }
        if(mission.Fase==Fase.Aterrizaje && mission.Altura<120 && !nearSeaAnnounced && mission.Resultado==null)
        {
            nearSeaAnnounced=true; incoming.Clear();
            QueueGround("Contacto con el agua inminente. Aseguren arneses y prepárense para el impacto.",true);
        }
        if(mission.Resultado!=null && !resultAnnounced)
        {
            resultAnnounced=true; incoming.Clear(); pendingReply=null; nextReception=clock;
            if(mission.Resultado.Tipo==TipoResultado.Exito)
                QueueGround("Amerizaje confirmado. Mantengan la escotilla cerrada. Rescate en camino; confirmen tripulación.",true);
            else if(mission.Resultado.Tipo==TipoResultado.AbortoSeguro)
                QueueGround("Aborto recibido. Equipos de recuperación avisados. Confirmen estado de la tripulación.",true);
            else { AwaitingAcknowledgement=false; interferenceAudio.Stop(); AddMessage("TIERRA","Se ha perdido el enlace de telemetría."); }
        }
    }

    static string PhaseMessage(Fase phase)
    {
        switch(phase)
        {
            case Fase.Despegue: return "Orión, ascenso nominal. Seguimos velocidad y carga estructural.";
            case Fase.Recorrido: return "Enlace de espacio profundo establecido. Mantengan vigilancia de energía y autonomía.";
            case Fase.Regreso: return "Orión, autorizada la reentrada. Puede haber interrupción de radio por plasma.";
            case Fase.Aterrizaje: return "Señal de descenso recibida. Verifiquen paracaídas; recuperación en el Pacífico.";
            default: return "Control de Tierra a Orión. Comprobación de comunicaciones. ¿Nos reciben?";
        }
    }

    void QueueGround(string message,bool acknowledgement)
    {
        if(incoming.Count>=6) incoming.Dequeue();
        incoming.Enqueue(new Transmission { Text=message,Acknowledge=acknowledgement });
    }
    void AddMessage(string sender,string message)
    {
        LatestMessage=sender+" · "+message;
        history.Enqueue("<color="+(sender=="ORIÓN"?"#FFAD70":"#59E8E0")+">"+sender+"</color>  "+message);
        while(history.Count>5) history.Dequeue();
        transcript.Clear();
        foreach(string entry in history) { if(transcript.Length>0) transcript.Append("\n\n"); transcript.Append(entry); }
        Transcript=transcript.ToString(); Revision++;
    }
    public bool Acknowledge()
    {
        if(!CanAcknowledge) return false;
        AwaitingAcknowledgement=false;
        AddMessage("ORIÓN",mission.Resultado?.Tipo==TipoResultado.Exito?"Copiado, Tierra. Cuatro astronautas a salvo. Esperamos recuperación.":"Copiado, Tierra. Mensaje recibido; procedemos.");
        radioAudio.PlayOneShot(transmitClip); return true;
    }
    public bool RequestStatus()
    {
        if(!CanRequestStatus) return false;
        requestAvailableAt=clock+5;
        AddMessage("ORIÓN","Tierra, solicitamos comprobación del enlace y estado de recuperación.");
        radioAudio.PlayOneShot(transmitClip);
        pendingReply=mission.Resultado?.Tipo==TipoResultado.Exito?"Baliza localizada. Permanezcan dentro de la cápsula; equipo de rescate en ruta.":"Telemetría recibida. "+PhaseMessage(mission.Fase);
        replyAt=clock+.9f; return true;
    }

    static AudioClip CreateRadioClip(string name,float duration,float frequency,bool noiseOnly)
    {
        const int rate=22050;
        var data=new float[Mathf.CeilToInt(rate*duration)];
        var random=new System.Random(noiseOnly?101:67);
        float low=0;
        for(int i=0;i<data.Length;i++)
        {
            float t=i/(float)rate;
            float white=(float)random.NextDouble()*2-1;
            low=Mathf.Lerp(low,white,.12f);
            float edge=Mathf.Min(1,Mathf.Min(t/.008f,(duration-t)/.025f));
            float burst=t<.08f || t>duration-.10f?1:.13f;
            float beep=!noiseOnly && t>.10f && t<.22f?Mathf.Sin(t*Mathf.PI*2*frequency)*.32f:0;
            data[i]=((white-low)*(noiseOnly?.28f:burst*.34f)+beep)*edge;
        }
        var clip=AudioClip.Create(name,data.Length,1,rate,false); clip.SetData(data,0); return clip;
    }
    void OnDestroy()
    {
        if(receiveClip!=null) Destroy(receiveClip);
        if(transmitClip!=null) Destroy(transmitClip);
        if(interferenceClip!=null) Destroy(interferenceClip);
    }
}
