using System;
using System.Linq;
using Artemis;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>uGUI traduce entradas a comandos; las reglas permanecen en Artemis.Domain.</summary>
public sealed partial class LaunchHud : MonoBehaviour
{
    static readonly Color Ink=new Color(.025f,.045f,.065f,.97f), Muted=new Color(.64f,.73f,.79f), Cyan=new Color(.35f,.91f,.88f), Orange=new Color(1,.49f,.2f), Surface=new Color(.065f,.115f,.15f);
    LaunchDemo view;
    RectTransform root,design,flight,result,details;
    TextMeshProUGUI detailsText;
    TextMeshProUGUI site,phase,status,telemetry,risk,evaluation,budget,flightInfo,log,report,countdown,timer,entryText,bankText;
    UnityEngine.UI.Button material,fuel,shield,crew,autonomy,launch,wait,accept,assist,primary,secondary,tertiary,engineContinue,engineAbort,pause,energy,bankAuto,chuteAuto,camera,audio,numberButton,generate;
    UnityEngine.UI.Button[] profiles;
    UnityEngine.UI.Button gameMode;
    UnityEngine.UI.Slider entrySlider,bankSlider;
    TMP_InputField seedInput;
    int number=1; Mision lastMission;
    float nextHudRefresh;
    Fase displayedPhase;
    ResultadoMision displayedResult;
    bool displayedPause,displayedWelcome;
    public void Initialize(LaunchDemo controller)
    {
        view=controller;
        var go=new GameObject("Mission Control",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        go.transform.SetParent(transform,false); root=go.GetComponent<RectTransform>(); go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1600,900); scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        // Keep every control within a centered safe frame on 16:9, 4:3 and ultrawide screens.
        root=Panel(go.transform,"Flight safe frame",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1600,900),Color.clear);
        if(FindAnyObjectByType<EventSystem>()==null)
        {
            var events=new GameObject("Launch Event System",typeof(EventSystem)); events.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
        }
        var header=Panel(root,"Header",new Vector2(0,1),new Vector2(28,-22),new Vector2(1100,100),Color.clear);
        Label(header,"AURORA  /  ARTEMIS",33,Color.white,0,0,700,43,true);
        Label(header,"DISEÑA. EXPLORA. REGRESA A CASA.",12,Cyan,2,46,750,22);
        site=Label(header,"",13,Muted,2,71,1100,24);
        gameMode=Button(root,"GameMode","",28,108,375,26,()=>view.ChooseGameMode(view.Simulation.EsCasual?ModoJuego.Realista:ModoJuego.Casual),false,12);
        camera=Button(root,"Camera","VISTA / CABINA",1155,28,190,40,()=>view.ToggleCamera());
        audio=Button(root,"Audio","AUDIO: ON",1357,28,215,40,()=>view.ToggleAudio());
        Button(root,"Details","PERFIL Y BITÁCORA",915,28,225,40,()=>details.gameObject.SetActive(!details.gameObject.activeSelf));

        design=Panel(root,"Vehicle design",new Vector2(0,1),new Vector2(28,-138),new Vector2(375,635),Ink);
        Label(design,"01  /  DISEÑO Y ABORDAJE",19,Cyan,20,16,340,28,true);
        profiles=new UnityEngine.UI.Button[8];
        string[] names={"01 · AL-LI / AVCOAT","02 · AL-LI / LOSETAS","03 · KERO / CORCHO","04 · ACERO / AVCOAT","05 · ACERO / KERO","06 · ALUMINIO 2024","07 · TITANIO","08 · FIBRA CARBONO"};
        for(int i=0;i<8;i++) { int n=i; profiles[i]=Button(design,"Profile"+(i+1),names[i],20+i%2*172,54+i/2*35,164,29,()=>view.ChooseProfile(n),false,11); }
        material=Button(design,"Material","",20,202,335,34,view.CycleMaterial);
        fuel=Button(design,"Propellant","",20,241,335,34,view.CycleFuel);
        shield=Button(design,"HeatShield","",20,280,335,34,view.CycleShield);
        evaluation=Label(design,"",14,Color.white,20,324,335,49);
        budget=Label(design,"",13,Cyan,20,378,335,44);
        crew=Button(design,"BoardCrew","ABORDAR 4 ASTRONAUTAS",20,429,335,34,()=>view.Simulation.Abordar());
        autonomy=Button(design,"Autonomy","",20,470,164,30,()=>view.Simulation.ConfigurarAutonomia(view.Simulation.Parametros.AutonomiaDias>=21?8:view.Simulation.Parametros.AutonomiaDias+1),false,12);
        assist=Button(design,"Guidance","",192,470,163,30,()=>view.Simulation.ConfigurarAsistencia(!view.Simulation.Asistencia),false,12);
        wait=Button(design,"WaitWeather","ESPERAR CLIMA",20,508,164,30,()=>view.Simulation.EsperarClima(),false,12);
        accept=Button(design,"AcceptWeather","ACEPTAR RIESGO",192,508,163,30,()=>view.Simulation.AceptarClima(),false,12);
        launch=Button(design,"LaunchButton","INICIAR DESPEGUE / ESPACIO",20,554,335,57,view.Launch,true,16);

        var planning=Panel(root,"Mission seed",new Vector2(1,1),new Vector2(-28,-138),new Vector2(270,128),Ink);
        Label(planning,"SEMILLA DE MISIÓN",11,Muted,16,12,235,19);
        seedInput=Input(planning,16,37,135,34,"2026");
        generate=Button(planning,"Generate","GENERAR",161,37,93,34,()=> { if(int.TryParse(seedInput.text,out int seed)) view.GenerateMission(seed,number); },false,12);
        numberButton=Button(planning,"MissionNumber","",16,82,238,29,()=> { number=number%8+1; if(int.TryParse(seedInput.text,out int seed)) view.GenerateMission(seed,number); },false,11);
        var t=Panel(root,"Telemetry",new Vector2(1,1),new Vector2(-28,-280),new Vector2(270,270),Ink);
        Label(t,"TELEMETRÍA  /  SI",12,Cyan,16,14,240,22);
        telemetry=Label(t,"",16,Color.white,16,48,238,206); telemetry.lineSpacing=8;
        var r=Panel(root,"Design assessment",new Vector2(1,1),new Vector2(-28,-564),new Vector2(270,209),Ink);
        riskPanel=r;
        Label(r,"PERFIL Y RIESGOS",12,Orange,16,13,238,22);
        risk=Label(r,"",13,Muted,16,43,238,153);

        flight=Panel(root,"Flight controls",new Vector2(0,1),new Vector2(28,-138),new Vector2(375,635),Ink);
        phase=Label(flight,"",21,Cyan,20,18,335,32,true);
        flightInfo=Label(flight,"",14,Color.white,20,65,335,115);
        primary=Button(flight,"PrimaryAction","",20,193,335,44,Primary,true);
        secondary=Button(flight,"SecondaryAction","",20,247,335,36,Secondary);
        tertiary=Button(flight,"TertiaryAction","",20,291,335,36,()=>view.Simulation.CorregirTrayectoria(-1));
        energy=Button(flight,"PowerSave","",20,335,335,33,()=>view.Simulation.AlternarEnergia());
        entryText=Label(flight,"",12,Muted,20,384,335,21);
        entrySlider=Slider(flight,"EntryAngle",20,413,335,-8,-4,-6,v=>view.Simulation.AjustarEntrada(v));
        bankText=Label(flight,"",12,Muted,20,444,335,21);
        bankSlider=Slider(flight,"BankAngle",20,475,335,0,180,0,v=>view.Simulation.AjustarAlabeo(v));
        bankAuto=Button(flight,"AutoBank","",20,510,164,30,()=>view.Simulation.AlternarAutoAlabeo(),false,11);
        chuteAuto=Button(flight,"AutoChutes","",192,510,163,30,()=>view.Simulation.AlternarAutoParacaidas(),false,11);
        engineContinue=Button(flight,"ContinueEngineOut","CONTINUAR ENGINE-OUT",20,555,213,44,()=>view.Simulation.ContinuarMotor(),true,12);
        engineAbort=Button(flight,"RejectEngineOut","ABORTAR",241,555,114,44,view.AbortMission);

        result=Panel(root,"Mission report",new Vector2(0,1),new Vector2(28,-138),new Vector2(375,635),Ink);
        Label(result,"INFORME DE MISIÓN",23,Cyan,20,20,335,34,true);
        report=Label(result,"",17,Color.white,20,75,335,204);
        log=Label(result,"",12,Muted,20,301,335,250);
        Button(result,"RetryMission","REPETIR MISIÓN",20,568,163,42,view.ResetDemo,true);
        Button(result,"NewMission","NUEVA SEMILLA",192,568,163,42,()=> { int seed=view.Simulation.Parametros.Semilla+1; seedInput.text=seed.ToString(); view.GenerateMission(seed,number); });

        var bottom=Panel(root,"Mission status",new Vector2(.5f,0),new Vector2(0,22),new Vector2(1544,94),Ink);
        Label(bottom,"CONTROL DE TIERRA",11,Cyan,20,12,700,20);
        status=Label(bottom,"",14,Color.white,20,39,730,45);
        Button(bottom,"Speed1","×1",787,44,52,34,()=>view.SetSpeedMultiplier(1));
        Button(bottom,"Speed5","×5",847,44,52,34,()=>view.SetSpeedMultiplier(5));
        Button(bottom,"Speed20","×20",907,44,52,34,()=>view.SetSpeedMultiplier(20));
        pause=Button(bottom,"Pause","PAUSA",980,44,90,34,view.TogglePause,false,12);
        Button(bottom,"AbortButton","ABORTAR",1088,44,113,34,view.AbortMission);
        timer=Label(bottom,"",13,Muted,787,12,740,21); timer.alignment=TextAlignmentOptions.Right;
        Button(bottom,"Reset","REINICIAR / R",1220,44,154,34,view.ResetDemo);
        Button(bottom,"WelcomeMenu","MENÚ / ESC",1386,44,138,34,view.ShowWelcome,false,11);
        countdown=Label(root,"",75,Color.white,600,260,480,140,true); countdown.alignment=TextAlignmentOptions.Center;
        details=Panel(root,"Mission details",new Vector2(.5f,.5f),Vector2.zero,new Vector2(820,670),Ink);
        details.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        Label(details,"EXPEDIENTE DE MISIÓN",24,Cyan,28,22,650,35,true);
        Button(details,"CloseDetails","CERRAR",674,20,118,38,()=>details.gameObject.SetActive(false));
        detailsText=Label(details,"",16,Color.white,28,82,764,560);
        details.gameObject.SetActive(false);
        BuildRadioHud();
        BuildWelcome(go.transform);
        ShowWelcome(true);
    }
    void Update()
    {
        if(view?.Simulation==null) return;
        var m=view.Simulation; var e=m.Evaluacion; bool prep=m.Fase==Fase.Abordaje && m.Resultado==null, counting=view.State==LaunchDemo.FlightState.Countdown;
        // Refresh telemetry at 10 Hz, and immediately on phase, pause or mission changes.
        if(Time.unscaledTime<nextHudRefresh && lastMission==m && displayedPhase==m.Fase && displayedResult==m.Resultado
            && displayedPause==view.PresentationPaused && displayedWelcome==view.WelcomeVisible) return;
        nextHudRefresh=Time.unscaledTime+.1f;
        displayedPhase=m.Fase; displayedResult=m.Resultado; displayedPause=view.PresentationPaused; displayedWelcome=view.WelcomeVisible;
        if(lastMission!=m) { lastMission=m; entrySlider.SetValueWithoutNotify((float)m.AnguloEntrada); bankSlider.SetValueWithoutNotify(0); }
        design.gameObject.SetActive(prep); flight.gameObject.SetActive(!prep && m.Resultado==null); result.gameObject.SetActive(m.Resultado!=null);
        site.text=$"{m.Parametros.Clima.ToUpperInvariant()}   /   LAT {m.Parametros.Latitud:F1}°   ·   INC {m.Parametros.Inclinacion:F1}°   ·   AZ {m.Parametros.Azimut:F1}°   /   ROTACIÓN {DinamicaOrbital.Rotacion(m.Datos,m.Parametros.Inclinacion):F0} m/s";
        status.text=view.Status;
        Set(gameMode,"MODO: "+(m.EsCasual?"CASUAL · AYUDAS ACTIVAS":"REALISTA")+(prep && !counting?"  › CAMBIAR":""));
        gameMode.interactable=prep && !counting;
        Set(material,"ESTRUCTURA  ›  "+m.Nave.MaterialId); Set(fuel,"COMBUSTIBLE  ›  "+m.Nave.CombustibleId.Replace('_','/')); Set(shield,"ESCUDO  ›  "+m.Nave.CapaId);
        for(int i=0;i<profiles.Length;i++) { profiles[i].interactable=prep && !counting; profiles[i].targetGraphic.color=e.Perfil?.id==i+1?new Color(.12f,.3f,.34f):Surface; }
        material.interactable=fuel.interactable=shield.interactable=crew.interactable=autonomy.interactable=assist.interactable=prep && !counting;
        assist.interactable=prep && !counting && !m.EsCasual;
        Set(crew,m.Abordados?"4 ASTRONAUTAS A BORDO":"ABORDAR 4 ASTRONAUTAS");
        Set(autonomy,"AUTONOMÍA: "+m.Parametros.AutonomiaDias+" d  +"); Set(assist,m.Asistencia?"GUÍA: TURISTA":"GUÍA: LABORATORIO");
        evaluation.text=$"M0 {e.MasaCargada/1000:N1} t   ·   Seca {e.MasaSeca/1000:N1} t\nΔv {e.DeltaV:N0} m/s   ·   TWR {e.Twr:F2}   ·   R {e.RelacionMasa:F2}";
        budget.text=$"COSTO {e.Costo:N1} / {m.Parametros.Presupuesto:N1} M USD\n"+(e.Viable?"DISEÑO VIABLE · tanques completos":e.Bloqueos.FirstOrDefault()); budget.color=e.Viable?Cyan:Orange;
        // Let the command explain missing crew/budget/weather instead of silently locking the button.
        launch.interactable=prep && !counting;
        Set(launch,counting?"ENCENDIDO EN CURSO":"INICIAR DESPEGUE / ESPACIO");
        wait.interactable=prep && !counting && m.Parametros.Clima=="Lluvioso"; accept.interactable=prep && !counting && m.Aplazamiento;
        Set(numberButton,$"MISIÓN {number} · "+(number>=5?"RS-25E +290 M":"RS-25 HEREDADOS"));
        seedInput.interactable=generate.interactable=numberButton.interactable=(prep && !counting) || m.Resultado!=null;
        string h=m.Altura>=1000?$"{m.Altura/1000:N1} km":$"{m.Altura:N0} m";
        telemetry.text=$"<size=27>{h}</size>\nVELOCIDAD   {m.Velocidad:N0} m/s\nMASA   {m.Masa/1000:N2} t\nCARGA   {m.FuerzaG:F2} G / {m.Q/1000:F1} kPa\nCALOR   {m.Calor/1000000:F2} MW/m²\nPROPELENTE   {m.PropelentePorcentaje:F1}%\nMOTORES   {m.Motores}/4 · {m.Throttle*100:F0}%";
        risk.text=(e.Perfil?.perfil??"COMBINACIÓN NO VIABLE")+"\n\n"+(e.Riesgos.Count>0?string.Join("\n",e.Riesgos):"Sin penalizaciones especiales. Riesgos SRB y motores activos.")+"\n\n"+(m.Asistencia?"Turista: crucero guiado.":"Laboratorio: trayectoria libre.");
        if(details.gameObject.activeSelf) detailsText.text=(e.Perfil?.perfil??"Configuración no certificada")+"\n"+(e.Perfil?.descripcion??"Selecciona uno de los ocho perfiles para habilitar el lanzamiento.")+"\n\n"+string.Join("\n",e.Bloqueos.Concat(e.Riesgos))+$"\n\nESTRUCTURA {m.Nave.Material.costo_estructura_musd:N1} M · ESCUDO {m.Nave.Capa.costo_musd:N1} M\nPROPELENTES {m.Nave.Combustible.costo_propelentes_musd:N1} M · AJUSTE CORE {m.Nave.Combustible.ajuste_costo_etapa_central_musd:N1} M\nTOTAL {e.Costo:N1} M USD · HARDWARE DESECHABLE\n\n"+string.Join("\n",m.Bitacora.Skip(Math.Max(0,m.Bitacora.Count-9)).Select(x=>x.ToString()));
        phase.text=((int)m.Fase+1).ToString("00")+" / "+m.Fase.ToString().ToUpperInvariant();
        flightInfo.text=$"ESCUDO   {m.Capsula.IntegridadEscudo*100:F0}%\nRCS   {m.Capsula.ReservaRcs:F0} m/s disponibles\nAUTONOMÍA   {m.AutonomiaRestante:F1} días\n"+(m.IcpsSeparado?m.Capsula.ServicioSeparado?"CABINA ORIÓN INDEPENDIENTE":"ORIÓN + MÓDULO DE SERVICIO":m.CoreSeparado?"ORIÓN + ICPS":"SLS · ASCENSO POR ETAPAS");
        ConfigureActions(m);
        entryText.text=m.Fase==Fase.Regreso || m.Fase==Fase.Aterrizaje
            ? $"TRAYECTORIA ACTUAL: {view.VisualFlightAngle:F1}° · HORIZONTE"
            : $"ÁNGULO ENTRADA: {m.AnguloEntrada+m.CorreccionEntrada:F1}°  [−6.5°, −5.5°]";
        bankText.text=$"ALABEO σ: {m.Alabeo:F0}° / "+(m.AutoAlabeo?"AUTOMÁTICO":"MANUAL");
        if(m.EsCasual && m.Fase==Fase.Recorrido) entryText.text="ENTRADA: "+(m.AnguloEntrada+m.CorreccionEntrada).ToString("F1")+"° · AYUDA AL CONFIRMAR";
        entrySlider.SetValueWithoutNotify((float)m.AnguloEntrada);
        entrySlider.interactable=m.Fase==Fase.Recorrido && m.Resultado==null; bankSlider.interactable=m.Fase==Fase.Regreso && m.Resultado==null && !m.EsCasual;
        bankAuto.interactable=m.Resultado==null && !m.EsCasual && (m.Fase==Fase.Recorrido || m.Fase==Fase.Regreso);
        chuteAuto.interactable=m.Resultado==null && !m.EsCasual;
        if(m.AutoAlabeo) bankSlider.SetValueWithoutNotify((float)m.Alabeo);
        Set(bankAuto,m.EsCasual?"ALABEO: ASISTIDO":m.AutoAlabeo?"ALABEO: AUTO":"ALABEO: MANUAL"); Set(chuteAuto,m.EsCasual?"PARACAÍDAS: ASISTIDOS":m.AutoParacaidas?"PARACAÍDAS: AUTO":"PARACAÍDAS: MANUAL");
        energy.interactable=m.Fase==Fase.Recorrido; Set(energy,m.AhorroEnergia?"AHORRO DE ENERGÍA: ACTIVO":"ACTIVAR AHORRO DE ENERGÍA");
        engineContinue.gameObject.SetActive(m.DecisionMotor); engineAbort.gameObject.SetActive(m.DecisionMotor);
        Set(pause,view.PresentationPaused?"REANUDAR":"PAUSA"); Set(camera,view.CloseCamera?"VISTA / COHETE":"VISTA / CABINA"); Set(audio,view.Muted?"AUDIO: OFF":"AUDIO: ON");
        timer.text="T+ "+TimeSpan.FromSeconds(m.Tiempo).ToString(@"dd\:hh\:mm\:ss")+$"   /   TIEMPO ×{view.EffectiveTimeRate:N0}   /   "+(view.PresentationPaused?"PAUSADO":m.DecisionMotor || m.ListaTli || m.ListaReentrada?"ESPERANDO DECISIÓN":"SIMULACIÓN");
        countdown.text=counting?Mathf.CeilToInt(view.Countdown).ToString("00"):"";
        if(view.PresentationPaused && !view.WelcomeVisible) status.text="EN PAUSA · Pulsa REANUDAR o P para continuar. Los ajustes siguen disponibles.";
        UpdateWelcome();
        UpdateRadioHud();
        if(m.Resultado!=null)
        {
            var r=m.Resultado;
            report.text=(r.Tipo==TipoResultado.Exito?"<color=#59E8E0>TRIPULACIÓN RECUPERADA</color>":r.Tipo==TipoResultado.AbortoSeguro?"ABORTO SEGURO":"<color=#FF7D33>MISIÓN PERDIDA</color>")+"\n\n"+r.Causa+$"\n\nCosto: {e.Costo:N1} M USD\nPérdida: {r.PerdidaMusd:N1} M USD\nMáximos: {m.MaxQ/1000:F1} kPa / {m.MaxG:F2} G";
            log.text=string.Join("\n\n",m.Bitacora.Skip(Math.Max(0,m.Bitacora.Count-5)).Select(x=>x.ToString()));
        }
    }
    void ConfigureActions(Mision m)
    {
        primary.interactable=secondary.interactable=tertiary.interactable=false;
        if(m.Fase==Fase.Despegue) { Set(primary,m.ListaTli?"CONFIRMAR INYECCIÓN TRANSLUNAR":"ASCENSO AUTOMÁTICO"); primary.interactable=m.ListaTli && !m.DecisionMotor; Set(secondary,"SRB / LAS / CORE: AUTO"); Set(tertiary,"VIGILA MAX-Q Y FUERZA G"); }
        if(m.Fase==Fase.Recorrido)
        {
            Set(primary,m.ListaReentrada?m.Capsula.ServicioSeparado?"INICIAR REINGRESO":"SEPARAR MÓDULO DE SERVICIO":m.IcpsSeparado?"RETORNO LIBRE EN CURSO":"SEPARAR ICPS · T+03:24 H");
            primary.interactable=m.ListaReentrada || !m.IcpsSeparado && m.Tiempo>=(double)m.Datos.Fisica["motores"]["ICPS_RL10C2"]["t_separacion_s"];
            Set(secondary,"TCM +8 m/s · ENTRADA +0.1°"); Set(tertiary,"TCM −8 m/s · ENTRADA −0.1°");
            secondary.interactable=tertiary.interactable=m.Capsula.ReservaRcs>=(double)m.Datos.Juego["correccion_tcm_ms"] && !m.FalloElectrico && !m.Capsula.ServicioSeparado;
        }
        if(m.Fase==Fase.Regreso) { Set(primary,"ESCUDO TÉRMICO ACTIVO"); Set(secondary,"MODULA EL ALABEO CON σ"); Set(tertiary,"LÍMITE: 4.5 G"); }
        if(m.Fase==Fase.Aterrizaje) { Set(primary,m.Principales?"PRINCIPALES DESPLEGADOS":"DESPLEGAR PRINCIPALES <2.9 KM"); primary.interactable=m.Drogues && !m.Principales && m.Altura<=2900; Set(secondary,m.Drogues?"DROGUES DESPLEGADOS":"DESPLEGAR DROGUES <7.3 KM"); secondary.interactable=!m.Drogues && m.Altura<=7300; Set(tertiary,"AMERIZAJE: 7.9–8.2 m/s"); }
    }
    void Primary() { var m=view.Simulation; if(m.ListaTli) m.InyectarTli(); else if(m.ListaReentrada) { if(!m.Capsula.ServicioSeparado) m.SepararServicio(); else m.Reingresar(); } else if(m.Fase==Fase.Recorrido) m.SepararIcps(); else if(m.Fase==Fase.Aterrizaje) m.DesplegarPrincipales(); }
    void Secondary() { var m=view.Simulation; if(m.Fase==Fase.Recorrido) m.CorregirTrayectoria(1); else if(m.Fase==Fase.Aterrizaje) m.DesplegarDrogues(); }
    static void Set(UnityEngine.UI.Button b,string text) { b.GetComponentInChildren<TextMeshProUGUI>().text=text; }
    static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=anchor; r.anchoredPosition=pos; r.sizeDelta=size;
        var image=go.GetComponent<UnityEngine.UI.Image>(); image.color=color; image.raycastTarget=false; return r;
    }
    static TextMeshProUGUI Label(Transform parent,string text,float size,Color color,float x,float y,float w,float h,bool bold=false)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h);
        var t=go.GetComponent<TextMeshProUGUI>(); t.text=text; t.font=TMP_Settings.defaultFontAsset; t.fontSize=size; t.color=color; t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal; t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.Normal; t.overflowMode=TextOverflowModes.Truncate; return t;
    }
    static UnityEngine.UI.Button Button(Transform parent,string name,string text,float x,float y,float w,float h,UnityAction action,bool accent=false,int size=14)
    {
        var r=Panel(parent,name,new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h),accent?Orange:Surface); var image=r.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget=true;
        var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=image; var c=b.colors; c.normalColor=Color.white; c.highlightedColor=new Color(1.18f,1.18f,1.18f); c.pressedColor=new Color(.7f,.9f,.93f); c.disabledColor=new Color(.55f,.6f,.65f,.7f); b.colors=c;
        b.onClick.AddListener(action); Label(r,text,size,accent?Ink:Cyan,6,0,w-12,h,true).alignment=TextAlignmentOptions.Center; return b;
    }
    static UnityEngine.UI.Slider Slider(Transform parent,string name,float x,float y,float w,float min,float max,float value,UnityAction<float> action)
    {
        var r=Panel(parent,name,new Vector2(0,1),new Vector2(x,-y),new Vector2(w,22),Surface); r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var area=Panel(r,"Handle area",new Vector2(0,0),new Vector2(9,0),new Vector2(w-18,22),Color.clear);
        var h=Panel(area,"Handle",new Vector2(0,.5f),Vector2.zero,new Vector2(18,22),Cyan); h.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var s=r.gameObject.AddComponent<UnityEngine.UI.Slider>(); s.minValue=min; s.maxValue=max; s.value=value; s.handleRect=h; s.targetGraphic=h.GetComponent<UnityEngine.UI.Image>(); s.onValueChanged.AddListener(action); return s;
    }
    static TMP_InputField Input(Transform parent,float x,float y,float w,float h,string value)
    {
        var r=Panel(parent,"Seed input",new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h),Surface); r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var t=Label(r,value,16,Color.white,8,3,w-16,h-6); var f=r.gameObject.AddComponent<TMP_InputField>(); f.textViewport=r; f.textComponent=t; f.contentType=TMP_InputField.ContentType.IntegerNumber; f.characterLimit=10; f.text=value; return f;
    }
}
