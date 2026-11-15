using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class LaunchHud
{
    RectTransform welcome, howTo;
    UnityEngine.UI.Button startMission;
    TextMeshProUGUI welcomeHint;

    public bool BlocksFlightShortcuts => view.WelcomeVisible || details.gameObject.activeSelf || seedInput.isFocused;

    public void HandleEscape()
    {
        if (seedInput.isFocused)
        {
            seedInput.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
        }
        else if (details.gameObject.activeSelf) details.gameObject.SetActive(false);
        else if (view.WelcomeVisible && howTo.gameObject.activeSelf) howTo.gameObject.SetActive(false);
        else if (view.WelcomeVisible) view.BeginMission();
        else view.ShowWelcome();
    }

    public void ShowWelcome(bool show)
    {
        root.gameObject.SetActive(!show);
        welcome.gameObject.SetActive(show);
        details.gameObject.SetActive(false);
        howTo.gameObject.SetActive(false);
        seedInput.DeactivateInputField();
        EventSystem.current?.SetSelectedGameObject(null);
        if (show) UpdateWelcome();
    }

    void BuildWelcome(Transform canvas)
    {
        welcome=Panel(canvas,"Welcome to Aurora",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1600,900),Color.clear);
        var scrim=Panel(welcome,"Welcome text backdrop",new Vector2(0,1),new Vector2(55,-100),new Vector2(650,690),new Color(.015f,.035f,.055f,.87f));
        Panel(scrim,"Accent",new Vector2(0,1),new Vector2(36,-40),new Vector2(60,4),Orange);
        Label(scrim,"AURORA  /  PROGRAMA ARTEMIS",16,Cyan,36,65,580,30,true);
        Label(scrim,"LA TIERRA\nTE ESPERA.",68,Color.white,32,122,594,170,true);
        Label(scrim,"Tu misión no termina en la Luna.",24,Cyan,36,315,570,40);
        Label(scrim,"Diseña tu cohete, lleva a cuatro astronautas al espacio y domina el regreso a casa.",21,Muted,36,373,555,90);
        startMission=Button(scrim,"StartMission","COMENZAR MISIÓN  ›",36,488,570,64,view.BeginMission,true,22);
        Button(scrim,"HowToPlay","CÓMO JUGAR",36,568,275,48,()=>howTo.gameObject.SetActive(true),false,15);
        Button(scrim,"WelcomeAudio","ACTIVAR / SILENCIAR AUDIO",326,568,280,48,view.ToggleAudio,false,13);
        welcomeHint=Label(scrim,"",13,Muted,36,640,575,24);
        Label(welcome,"01   DISEÑA     /     02   EXPLORA     /     03   REGRESA",16,Cyan,83,824,1100,30,true);
        Label(welcome,"UNA MISIÓN.\nCUATRO VIDAS.\nUN HOGAR.",22,Color.white,1190,665,340,120,true);

        howTo=Panel(welcome,"How to play",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1060,620),Ink);
        howTo.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        Label(howTo,"TU PRIMERA MISIÓN",32,Cyan,38,32,850,45,true);
        Button(howTo,"CloseHowTo","CERRAR",870,30,150,45,()=>howTo.gameObject.SetActive(false));
        string[] titles={"01 / PREPARA", "02 / EXPLORA", "03 / REGRESA"};
        string[] descriptions={
            "Elige un perfil viable y aborda a los astronautas.\n\nSi llueve, espera otra ventana o acepta el riesgo. Después, inicia el despegue.",
            "Confirma la inyección translunar cuando aparezca.\n\nControla la energía y las correcciones de trayectoria. La telemetría te indica cuándo actuar.",
            "Separa el módulo de servicio. Ajusta la entrada entre −6.5° y −5.5°.\n\nLa flecha muestra la dirección de la cápsula. Controla el alabeo o conserva la ayuda automática."};
        for(int i=0;i<3;i++)
        {
            var card=Panel(howTo,"Step "+i,new Vector2(0,1),new Vector2(38+i*332,-110),new Vector2(318,335),Surface);
            Label(card,titles[i],20,Orange,18,20,282,36,true);
            Label(card,descriptions[i],18,Color.white,18,76,282,245);
        }
        Label(howTo,"F  ABORDAR     ·     ESPACIO  LANZAR     ·     P  PAUSA     ·     R  REINICIAR     ·     ESC  MENÚ",15,Cyan,38,475,984,30,true);
        Label(howTo,"Los controles se habilitan según la fase. El ángulo se prepara durante el crucero; el alabeo se ajusta durante la reentrada. El menú suspende el vuelo y permite continuar.",17,Muted,38,522,984,70);
        howTo.gameObject.SetActive(false);
    }

    void UpdateWelcome()
    {
        if (welcome == null || !view.WelcomeVisible) return;
        bool underway=view.State!=LaunchDemo.FlightState.Preparing || view.Simulation.Abordados;
        Set(startMission,underway ? "CONTINUAR MISIÓN  ›" : "COMENZAR MISIÓN  ›");
        welcomeHint.text=view.Muted ? "AUDIO SILENCIADO · ESC PARA ENTRAR" : "AUDIO ACTIVO · ESC PARA ENTRAR";
    }
}
