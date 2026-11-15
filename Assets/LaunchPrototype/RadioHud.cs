using Artemis;
using TMPro;
using UnityEngine;

public sealed partial class LaunchHud
{
    RectTransform radioPanel,radioConsole,riskPanel;
    TextMeshProUGUI radioLink,radioMessage,radioHistory,radioConsoleLink;
    UnityEngine.UI.Button acknowledgeRadio,requestRadio,consoleAcknowledge,consoleRequest;
    int radioRevision=-1;

    void BuildRadioHud()
    {
        radioPanel=Panel(root,"Earth capsule radio",new Vector2(1,1),new Vector2(-28,-564),new Vector2(270,209),Ink);
        Panel(radioPanel,"Radio accent",new Vector2(0,1),new Vector2(0,0),new Vector2(3,209),Cyan);
        Label(radioPanel,"TIERRA  ↔  ORIÓN",15,Cyan,16,12,238,23,true);
        radioLink=Label(radioPanel,"",10,Muted,16,39,238,18);
        radioMessage=Label(radioPanel,"",12,Color.white,16,60,238,79);
        acknowledgeRadio=Button(radioPanel,"RadioAcknowledge","RECIBIDO",16,145,114,30,()=>view.Radio.Acknowledge(),true,11);
        requestRadio=Button(radioPanel,"RadioStatus","ESTADO",140,145,114,30,()=>view.Radio.RequestStatus(),false,11);
        Button(radioPanel,"OpenRadio","ABRIR REGISTRO DE RADIO  ›",16,182,238,20,()=>radioConsole.gameObject.SetActive(true),false,10);

        radioConsole=Panel(root,"Radio communications console",new Vector2(.5f,.5f),new Vector2(35,0),new Vector2(700,600),Ink);
        radioConsole.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        Label(radioConsole,"COMUNICACIÓN  /  TIERRA – ORIÓN",22,Cyan,24,22,530,32,true);
        Button(radioConsole,"CloseRadio","CERRAR",558,20,118,36,()=>radioConsole.gameObject.SetActive(false),false,12);
        radioConsoleLink=Label(radioConsole,"",13,Muted,24,68,650,25);
        radioHistory=Label(radioConsole,"",16,Color.white,24,112,652,394);
        consoleAcknowledge=Button(radioConsole,"ConsoleAcknowledge","CONFIRMAR RECEPCIÓN",24,530,315,44,()=>view.Radio.Acknowledge(),true,13);
        consoleRequest=Button(radioConsole,"ConsoleStatus","SOLICITAR ESTADO",355,530,321,44,()=>view.Radio.RequestStatus(),false,13);
        radioConsole.gameObject.SetActive(false);
    }
    void UpdateRadioHud()
    {
        if(view.Radio==null || radioPanel==null) return;
        bool active=view.Simulation.Fase!=Fase.Abordaje || view.State==LaunchDemo.FlightState.Countdown;
        radioPanel.gameObject.SetActive(active); riskPanel.gameObject.SetActive(!active);
        if(!active) radioConsole.gameObject.SetActive(false);
        var radio=view.Radio;
        radioLink.text=radio.LinkStatus+"  /  "+radio.SignalPercent+"%";
        radioLink.color=radio.Blackout?Orange:Cyan;
        radioConsoleLink.text=radioLink.text+"  ·  "+(radio.AwaitingAcknowledgement?"RECEPCIÓN PENDIENTE":"CANAL DE MISIÓN");
        if(radioRevision!=radio.Revision)
        {
            radioRevision=radio.Revision;
            radioMessage.text=radio.LatestMessage;
            radioHistory.text=radio.Transcript;
        }
        acknowledgeRadio.interactable=consoleAcknowledge.interactable=radio.CanAcknowledge;
        requestRadio.interactable=consoleRequest.interactable=radio.CanRequestStatus;
    }
}
