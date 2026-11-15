using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Artemis
{
    public enum Fase { Abordaje, Despegue, Recorrido, Regreso, Aterrizaje }
    public enum TipoResultado { Exito, AbortoSeguro, PerdidaTotal }
    public sealed class ResultadoMision
    {
        public TipoResultado Tipo; public string Causa; public Fase Fase; public double PerdidaMusd, Duracion, VelocidadAmerizaje;
    }
    public sealed class EventoMision
    {
        public double Tiempo; public string Texto; public bool Alerta;
        public override string ToString() => TimeSpan.FromSeconds(Tiempo).ToString(@"dd\:hh\:mm\:ss")+"  "+Texto;
    }
    public interface IFaseMision { Fase Id { get; } void Avanzar(Mision m,double dt); }
    public sealed class FaseAbordaje : IFaseMision { public Fase Id=>Fase.Abordaje; public void Avanzar(Mision m,double dt) {} }
    public sealed class FaseDespegue : IFaseMision { public Fase Id=>Fase.Despegue; public void Avanzar(Mision m,double dt) => m.AvanzarAscenso(dt); }
    public sealed class FaseRecorrido : IFaseMision { public Fase Id=>Fase.Recorrido; public void Avanzar(Mision m,double dt) => m.AvanzarRecorrido(dt); }
    public sealed class FaseRegreso : IFaseMision { public Fase Id=>Fase.Regreso; public void Avanzar(Mision m,double dt) => m.AvanzarReingreso(dt); }
    public sealed class FaseAterrizaje : IFaseMision { public Fase Id=>Fase.Aterrizaje; public void Avanzar(Mision m,double dt) => m.AvanzarDescenso(dt); }
    public sealed class MaquinaEstadosMision
    {
        readonly IFaseMision[] fases={new FaseAbordaje(),new FaseDespegue(),new FaseRecorrido(),new FaseRegreso(),new FaseAterrizaje()};
        public IFaseMision Actual { get; private set; }
        public MaquinaEstadosMision() { Actual=fases[0]; }
        public void Cambiar(Fase fase) { Actual=fases[(int)fase]; }
    }
    public sealed class SistemaFallos
    {
        readonly RepositorioDatos d; readonly IRandom random;
        readonly HashSet<int> motoresFallados=new HashSet<int>();
        int ultimoSegundo=-1;
        bool srbComprobado, maxqComprobado;
        public SistemaFallos(RepositorioDatos datos,IRandom rng) { d=datos; random=rng; }
        public bool Probabilidad(double p) => random.Next()<Matematica.Clamp(p,0,1);
        public static double Acumulada(double probabilidad,int motores,double segundos) => 1-Math.Pow(1-probabilidad,motores*segundos);
        public void Ascenso(Mision m)
        {
            double mult=(double)d.Fisica["clima"][m.Parametros.Clima]["riesgo_ascenso"];
            if(!srbComprobado)
            {
                srbComprobado=true;
                if(Probabilidad((double)d.Riesgos["p_srb"]*mult)) { m.Terminar(TipoResultado.PerdidaTotal,"Fallo catastrófico SRB."); return; }
            }
            int segundo=(int)Math.Floor(m.Tiempo);
            if(m.EstadoAscenso[5]>0 && segundo>ultimoSegundo)
            {
                // Una muestra por segundo por motor, independiente del framerate y aceleración temporal.
                for(int s=ultimoSegundo+1;s<=segundo;s++)
                    for(int motor=0;motor<(int)d.Riesgos["motores"];motor++)
                        if(!motoresFallados.Contains(motor) && Probabilidad((double)d.Riesgos["p_motor_segundo"]*mult))
                        { motoresFallados.Add(motor); m.FalloMotor(motor+1); if(m.Resultado!=null) return; }
                ultimoSegundo=segundo;
            }
            double fin=(double)d.Fisica["motores"]["RS25x4"]["ventana_maxq_s"][1];
            if(!maxqComprobado && m.Tiempo>=fin)
            {
                maxqComprobado=true;
                if(m.Nave.MaterialId=="CFRP" && Probabilidad((double)d.Riesgos["p_agrietamiento_maxq"]*m.MaxQ/m.LimiteQ*mult))
                    m.Terminar(TipoResultado.PerdidaTotal,"Agrietamiento estructural por Max-Q.");
            }
        }
        public void Recorrido(Mision m)
        {
            if(m.Nave.CapaId=="Losetas" && Probabilidad((double)d.Riesgos["p_impacto"]))
            { m.Capsula.IntegridadEscudo=Math.Max(0,m.Capsula.IntegridadEscudo-(double)d.Riesgos["dano_escudo_impacto"]); m.Emitir("Impacto: escudo térmico dañado. Revisa el margen térmico.",true); }
            if(m.Parametros.Clima=="Eclipse" && !m.AhorroEnergia && Probabilidad((double)d.Riesgos["p_electrico"]*(double)d.Fisica["clima"]["Eclipse"]["riesgo_electrico"]))
            { m.FalloElectrico=true; m.Emitir("Anomalía eléctrica: activa ahorro de energía para recuperar sistemas.",true); }
        }
    }
    public sealed class Mision
    {
        public RepositorioDatos Datos { get; }
        public ParametrosIniciales Parametros { get; }
        public DisenadorNave Diseno { get; }
        public Cohete Nave=>Diseno.Cohete;
        public EvaluacionCohete Evaluacion { get; private set; }
        public CapsulaOrion Capsula { get; }=new CapsulaOrion();
        public MaquinaEstadosMision Maquina { get; }=new MaquinaEstadosMision();
        public Fase Fase=>Maquina.Actual.Id;
        public SistemaFallos Fallos { get; }
        public ResultadoMision Resultado { get; private set; }
        public readonly List<EventoMision> Bitacora=new List<EventoMision>();
        public event Action<EventoMision> OnEvento;
        public event Action<Fase> OnFaseCambiada;
        public event Action<int> OnEngineOut;
        public event Action<ResultadoMision> OnMisionAbortada;
        public event Action<double> OnPresupuestoExcedido;
        public double Tiempo { get; private set; }
        public double Altura { get; private set; }
        public double Velocidad { get; private set; }
        public double Masa { get; private set; }
        public double Q { get; private set; }
        public double FuerzaG { get; private set; }
        public double Calor { get; private set; }
        public double MaxQ { get; private set; }
        public double MaxG { get; private set; }
        public double AnguloEntrada { get; private set; }=-6;
        public double Alabeo { get; private set; }
        public double CorreccionEntrada { get; private set; }
        public bool AutoAlabeo { get; private set; }=true;
        public bool AutoParacaidas { get; private set; }=true;
        public bool Asistencia { get; private set; }
        public bool Pausada { get; private set; }
        public bool DecisionMotor { get; private set; }
        public bool Aplazamiento { get; private set; }
        public bool Abordados { get; private set; }
        public bool AhorroEnergia { get; private set; }
        public bool FalloElectrico { get; internal set; }
        public bool ListaTli { get; private set; }
        public bool TliRealizada { get; private set; }
        public bool ListaReentrada { get; private set; }
        public bool SrbSeparado=>ascenso!=null && ascenso.SrbSeparado;
        public bool LasSeparado=>ascenso!=null && ascenso.LasSeparado;
        public bool CoreSeparado=>ascenso!=null && ascenso.CoreSeparado;
        public bool IcpsSeparado=>ascenso!=null && ascenso.IcpsSeparado;
        public bool Drogues=>descenso!=null && descenso.Drogues;
        public bool Principales=>descenso!=null && descenso.Principales;
        public int Motores=>ascenso?.Motores ?? (int)Datos.Riesgos["motores"];
        public double Throttle=>ascenso?.Throttle??0;
        public double LimiteQ=>(double)Datos.Fisica["limites"]["q_max_pa_AlLi"]*(double)Datos.Juego["factores_q"][Nave.MaterialId];
        public double AutonomiaRestante=>Parametros.AutonomiaDias-Tiempo/86400;
        public double PropelentePorcentaje=>EstadoAscenso==null ? 100 : (EstadoAscenso[4]+EstadoAscenso[5]+EstadoAscenso[6])/(Nave.Combustible.prop_total_t*1000)*100;
        public double[] EstadoAscenso { get; private set; }
        public double[] EstadoOrbital { get; private set; }
        public double[] EstadoReingreso { get; private set; }
        public double[] EstadoDescenso { get; private set; }
        public string Mensaje { get; private set; }="Diseña la nave y aborda a la tripulación.";
        public double Paso=>Fase==Fase.Recorrido ? (double)Datos.Juego["paso_orbital_s"] : (double)Datos.Juego["dt_s"];
        DinamicaAscenso ascenso; DinamicaOrbital orbital; DinamicaReingreso reingreso; DinamicaDescenso descenso;
        double acumulador, inicioRecorrido, siguienteEvento, inicioReingreso, maxAltura;
        bool climaComprobado, meco, avisoMaxQ, lunaVisitada;
        public Mision(RepositorioDatos datos,ParametrosIniciales parametros,IRandom random=null)
        {
            Datos=datos; Parametros=parametros; Diseno=new DisenadorNave(datos);
            Asistencia=(bool)datos.Juego["asistencia"]; Capsula.ReservaRcs=(double)datos.Juego["rcs_reserva_ms"];
            Fallos=new SistemaFallos(datos,random??new RandomSemilla(parametros.Semilla)); Evaluar();
        }
        public void Evaluar()
        {
            Evaluacion=new EvaluadorCohete(Datos).Evaluar(Nave,Parametros); Masa=Evaluacion.MasaCargada;
            if(Evaluacion.Costo>Parametros.Presupuesto) OnPresupuestoExcedido?.Invoke(Evaluacion.Costo-Parametros.Presupuesto);
        }
        public bool Seleccionar(string material,string combustible,string capa)
        { if(Fase!=Fase.Abordaje || Resultado!=null) return false; Diseno.Seleccionar(material,combustible,capa); Evaluar(); return true; }
        public void ConfigurarAutonomia(double dias) { if(Fase!=Fase.Abordaje) return; Parametros.AutonomiaDias=Matematica.Clamp(dias,(double)Datos.Juego["autonomia_min_dias"],(double)Datos.Juego["autonomia_max_dias"]); Evaluar(); }
        public void ConfigurarAsistencia(bool enabled) { if(Fase==Fase.Abordaje) Asistencia=enabled; }
        public void Abordar() { if(Fase!=Fase.Abordaje || Resultado!=null) return; Abordados=true; Emitir("4 astronautas a bordo · autonomía "+Parametros.AutonomiaDias+" días."); }
        public bool PrepararLanzamiento()
        {
            if(Fase!=Fase.Abordaje || Resultado!=null) return false;
            Evaluar();
            if(!Evaluacion.Viable) { Emitir(string.Join(" ",Evaluacion.Bloqueos),true); return false; }
            if(!Abordados) { Emitir("Aborda a los cuatro astronautas antes de lanzar.",true); return false; }
            if(!climaComprobado) { climaComprobado=true; Aplazamiento=Fallos.Probabilidad((double)Datos.Fisica["clima"][Parametros.Clima]["aplazamiento"]); }
            if(Aplazamiento) { Emitir("Ventana meteorológica cerrada: espera o acepta el riesgo.",true); return false; }
            return true;
        }
        public void EsperarClima()
        {
            if(Fase!=Fase.Abordaje || Resultado!=null) return;
            Parametros.Clima="Soleado"; Aplazamiento=false; climaComprobado=true; Evaluar(); Emitir("Nueva ventana: clima soleado, tanques reacondicionados.");
        }
        public void AceptarClima() { if(Fase==Fase.Abordaje && Resultado==null) { Aplazamiento=false; climaComprobado=true; Emitir("Riesgo meteorológico aceptado."); } }
        public bool Lanzar()
        {
            if(!PrepararLanzamiento()) return false;
            ascenso=new DinamicaAscenso(Datos,Nave,Parametros.Clima) { Inclinacion=Parametros.Inclinacion };
            EstadoAscenso=ascenso.Inicial(DinamicaOrbital.Rotacion(Datos,Parametros.Inclinacion));
            Cambiar(Fase.Despegue); Emitir("Despegue · motores nominales."); return true;
        }
        public void Pausar() { if(Resultado==null) Pausada=!Pausada; }
        public void Avanzar(double segundos)
        {
            if(segundos<=0 || double.IsNaN(segundos) || double.IsInfinity(segundos) || Resultado!=null || Fase==Fase.Abordaje || Pausada || DecisionMotor || ListaTli || ListaReentrada) return;
            acumulador+=segundos;
            int iteraciones=0;
            while(acumulador+1e-9>=Paso && iteraciones++<10000 && Resultado==null && !DecisionMotor && !ListaTli && !ListaReentrada)
            {
                double dt=Paso; acumulador-=dt; Maquina.Actual.Avanzar(this,dt);
                if(AutonomiaRestante<=0) Terminar(TipoResultado.PerdidaTotal,"Autonomía agotada.");
            }
            // Las decisiones no acumulan tiempo oculto que luego salte tras un clic.
            if(DecisionMotor || ListaTli || ListaReentrada || Resultado!=null) acumulador=0;
        }
        internal void AvanzarAscenso(double dt)
        {
            // Corte preventivo: una etapa al mínimo de acelerador puede superar G antes de vaciarse.
            // Si ya alcanzó velocidad orbital, MECO conserva el límite y desecha el remanente con el núcleo.
            ascenso.Derivada(Tiempo,EstadoAscenso);
            double circular=DinamicaOrbital.Circular((double)Datos.Fisica["mu_tierra"],(double)Datos.Fisica["R_tierra"]+Altura);
            if(!CoreSeparado && Tiempo>129 && ascenso.FuerzaG>=(double)Datos.Fisica["limites"]["g_max_ascenso"]-(double)Datos.Juego["g_tolerancia_numerica"] && ascenso.Throttle<=(double)Datos.Fisica["motores"]["RS25x4"]["throttle_min"] && Velocidad>=circular)
            {
                meco=true; ascenso.CoreSeparado=true; EstadoAscenso[5]=0; Masa=ascenso.Masa(EstadoAscenso); FuerzaG=Q=0;
                ListaTli=true; Emitir("MECO preventivo por límite G · núcleo separado con propelente residual."); return;
            }
            var y=EstadoAscenso; ascenso.Paso(ref y,Tiempo,dt); EstadoAscenso=y; Tiempo+=dt;
            Altura=Math.Max(0,y[1]); Velocidad=Matematica.Hipot(y[2],y[3]); Masa=ascenso.Masa(y); Q=ascenso.Q; FuerzaG=ascenso.FuerzaG;
            MaxQ=Math.Max(Q,MaxQ); MaxG=Math.Max(FuerzaG,MaxG);
            if(!avisoMaxQ && Tiempo>=45) { avisoMaxQ=true; Emitir("Max-Q · reducción automática de potencia."); }
            Fallos.Ascenso(this); if(Resultado!=null) return;
            if(Q>LimiteQ) { if(Q>LimiteQ*(double)Datos.Juego["destruccion_q_factor"]) Terminar(TipoResultado.PerdidaTotal,"Límite estructural de presión dinámica excedido."); else Abortar("Presión dinámica fuera del límite."); return; }
            if(FuerzaG>(double)Datos.Fisica["limites"]["g_max_ascenso"]+(double)Datos.Juego["g_tolerancia_numerica"]) { Abortar("Fuerza G de ascenso excesiva."); return; }
            if(!SrbSeparado && Tiempo+1e-6>=(double)Datos.Fisica["motores"]["SRB"]["t_separacion_s"])
            { ascenso.SrbSeparado=true; y[4]=0; Emitir("SRB separados · dos propulsores desechados."); }
            if(!LasSeparado && Tiempo+1e-6>=(double)Datos.Fisica["motores"]["LAS"]["t_eyeccion_s"])
            { ascenso.LasSeparado=true; Emitir("Torre LAS eyectada."); }
            if(!meco && y[5]<=0) { meco=true; Emitir("MECO · propelente central agotado."); }
            if(!CoreSeparado && (meco || Tiempo>=(double)Datos.Fisica["motores"]["RS25x4"]["t_separacion_core_s"]) && Tiempo>129)
            {
                ascenso.CoreSeparado=true; y[5]=0; Masa=ascenso.Masa(y); FuerzaG=Q=0;
                ListaTli=true; Emitir("Etapa central separada · confirma la inyección translunar.");
            }
            if(y[1]<-1 && Tiempo>5) Terminar(TipoResultado.PerdidaTotal,"Impacto contra la superficie durante el ascenso.");
        }
        public void FalloMotor(int motor)
        {
            if(Fase!=Fase.Despegue || Resultado!=null) return;
            ascenso.Motores--;
            if(ascenso.Motores<(int)Datos.Riesgos["motores"]-1) { Abortar("Fallo múltiple de motores."); return; }
            DecisionMotor=true; Emitir("Motor #"+motor+" apagado por anomalía térmica. Consumo +5%. ¿Continuar o abortar?",true); OnEngineOut?.Invoke(motor);
        }
        public void ContinuarMotor()
        {
            if(!DecisionMotor || Resultado!=null) return;
            DecisionMotor=false;
            double disponible=DeltaVRestante();
            double requerido=Math.Max(0,Datos.Juego["dv_requerido_ms"].Value<double>()*(1-(double)Datos.Juego["margen_dv"])-Velocidad);
            if(disponible<requerido) { Abortar("Engine-out: delta-v restante insuficiente."); return; }
            Emitir("Engine-out aceptado · margen reducido y consumo +5%.");
        }
        public double DeltaVRestante()
        {
            if(EstadoAscenso==null) return Evaluacion.DeltaV;
            double g=(double)Datos.Fisica["g0"], coreDry=(double)Datos.Fisica["masas"]["core_kg"]*Nave.Material.factor;
            double upper=(double)Datos.Fisica["masas"]["icps_kg"]*Nave.Material.factor+(double)Datos.Fisica["masas"]["orion_esm_kg"]+Nave.Capa.masa_t*1000;
            double pc=EstadoAscenso[5], pi=EstadoAscenso[6], ps=EstadoAscenso[4];
            double dv=(double)Datos.Fisica["motores"]["ICPS_RL10C2"]["isp"]*g*Math.Log((upper+pi)/upper);
            if(!CoreSeparado) dv+=(double)Datos.Fisica["motores"]["RS25x4"]["isp_vacio"]*Nave.Combustible.factor_isp_etapa_central*g*Math.Log((coreDry+upper+pi+pc)/(coreDry+upper+pi));
            if(!SrbSeparado) dv+=(double)Datos.Fisica["motores"]["SRB"]["isp_vacio"]*g*Math.Log((Masa)/(Math.Max(1,Masa-ps)));
            return dv;
        }
        public bool InyectarTli()
        {
            if(!ListaTli || Resultado!=null || DecisionMotor) return false;
            double r=(double)Datos.Fisica["R_tierra"]+Math.Max(Altura,(double)Datos.Juego["orbita_aparcamiento_m"]);
            double a=(r+(double)Datos.Juego["distancia_luna_m"])/2, mu=(double)Datos.Fisica["mu_tierra"];
            double v=DinamicaOrbital.Transferencia(mu,r,a);
            double dv=Matematica.Hipot(v-EstadoAscenso[2],EstadoAscenso[3]), isp=(double)Datos.Fisica["motores"]["ICPS_RL10C2"]["isp"];
            double consumo=Masa*(1-Math.Exp(-dv/(isp*(double)Datos.Fisica["g0"])));
            if(consumo>EstadoAscenso[6]) { Abortar("ICPS sin margen para la inyección translunar."); return false; }
            EstadoAscenso[6]-=consumo; ListaTli=false; TliRealizada=true; Masa=ascenso.Masa(EstadoAscenso);
            inicioRecorrido=Tiempo; siguienteEvento=Tiempo+DuracionCrucero/(int)Datos.Juego["eventos_recorrido"];
            orbital=new DinamicaOrbital(Datos) { FaseLuna=Math.PI-2*Math.PI*(Tiempo+DuracionCrucero/2)/(double)Datos.Juego["periodo_lunar_s"] };
            EstadoOrbital=new[] {r,0.0,0.0,v}; maxAltura=Altura;
            Cambiar(Fase.Recorrido); Emitir("TLI confirmada · Δv "+dv.ToString("N0")+" m/s. "+(Asistencia?"Navegación guiada de retorno libre.":"Propagación gravitatoria libre.")); return true;
        }
        double DuracionCrucero=>(double)Datos.Juego["duracion_mision_dias"]*86400;
        internal void AvanzarRecorrido(double dt)
        {
            EstadoOrbital=IntegradorRK4.Paso(orbital,Tiempo,EstadoOrbital,dt); Tiempo+=dt;
            double r=Matematica.Hipot(EstadoOrbital[0],EstadoOrbital[1]);
            // Modo turista: referencia de misión comprimida. El vector CR3BP se conserva para el modo laboratorio.
            // No se presenta este guiado como una solución balística certificada ni como consumo RCS real.
            double progreso=Matematica.Clamp((Tiempo-inicioRecorrido)/DuracionCrucero,0,1);
            if(Asistencia)
            {
                double h0=(double)Datos.Fisica["capsula"]["altura_interfaz_m"], hmax=(double)Datos.Juego["distancia_luna_m"]-(double)Datos.Fisica["R_tierra"];
                Altura=h0+(hmax-h0)*Math.Sin(progreso*Math.PI);
                double a=((double)Datos.Fisica["R_tierra"]+h0+(double)Datos.Juego["distancia_luna_m"])/2;
                Velocidad=DinamicaOrbital.Transferencia((double)Datos.Fisica["mu_tierra"],(double)Datos.Fisica["R_tierra"]+Altura,a);
            }
            else { Altura=Math.Max(0,r-(double)Datos.Fisica["R_tierra"]); Velocidad=Matematica.Hipot(EstadoOrbital[2],EstadoOrbital[3]); }
            Q=FuerzaG=0; maxAltura=Math.Max(maxAltura,Altura);
            if(!IcpsSeparado && Tiempo>=(double)Datos.Fisica["motores"]["ICPS_RL10C2"]["t_separacion_s"]) SepararIcps();
            if(!lunaVisitada && progreso>=0.5) { lunaVisitada=true; Emitir("Sobrevuelo lunar · tramo de regreso a la Tierra."); }
            if(Tiempo>=siguienteEvento) { siguienteEvento+=DuracionCrucero/(int)Datos.Juego["eventos_recorrido"]; Fallos.Recorrido(this); }
            if(Asistencia && progreso>=1 || !Asistencia && Altura<=(double)Datos.Fisica["capsula"]["altura_interfaz_m"] && maxAltura>Altura*2)
            { ListaReentrada=true; Emitir("Retorno terrestre · separa servicio y configura el corredor de reingreso."); }
            if(!Asistencia && Tiempo-inicioRecorrido>DuracionCrucero*1.5) Abortar("Trayectoria libre fuera de la ventana de retorno.");
        }
        public void SepararIcps()
        {
            if(Fase!=Fase.Recorrido || IcpsSeparado || Resultado!=null || Tiempo<(double)Datos.Fisica["motores"]["ICPS_RL10C2"]["t_separacion_s"]) return;
            ascenso.IcpsSeparado=true; EstadoAscenso[6]=0; Masa=ascenso.Masa(EstadoAscenso); Emitir("ICPS separado · primera cúpula liberada.");
        }
        public void SepararServicio()
        {
            if(!ListaReentrada || !IcpsSeparado || Capsula.ServicioSeparado || Resultado!=null) return;
            Capsula.ServicioSeparado=true; Masa=(double)Datos.Juego["masa_capsula_sin_escudo_kg"]+Nave.Capa.masa_t*1000; Emitir("Módulo de servicio separado · queda la cabina de los astronautas.");
        }
        public void CorregirTrayectoria(int direccion)
        {
            if(Fase!=Fase.Recorrido || Resultado!=null || FalloElectrico) return;
            double uso=(double)Datos.Juego["correccion_tcm_ms"];
            if(Capsula.ReservaRcs<uso) { Emitir("Reserva RCS insuficiente.",true); return; }
            Capsula.ReservaRcs-=uso; CorreccionEntrada=Matematica.Clamp(CorreccionEntrada+Math.Sign(direccion)*0.1,-1,1);
            double speed=Matematica.Hipot(EstadoOrbital[2],EstadoOrbital[3]);
            if(speed>0) { EstadoOrbital[2]+=Math.Sign(direccion)*uso*EstadoOrbital[2]/speed; EstadoOrbital[3]+=Math.Sign(direccion)*uso*EstadoOrbital[3]/speed; }
            Emitir("TCM "+(direccion>0?"+":"−")+uso+" m/s · ajuste de entrada "+CorreccionEntrada.ToString("+0.0;-0.0;0.0")+"°.");
        }
        public void AlternarEnergia()
        { if(Fase!=Fase.Recorrido || Resultado!=null) return; AhorroEnergia=!AhorroEnergia; if(AhorroEnergia) FalloElectrico=false; Emitir(AhorroEnergia?"Modo de ahorro: respaldo eléctrico activo.":"Paneles y sistemas en modo normal."); }
        public void AjustarEntrada(double grados) { if(Fase==Fase.Recorrido && Resultado==null) AnguloEntrada=Matematica.Clamp(grados,-8,-4); }
        public void AjustarAlabeo(double grados) { if(Resultado!=null) return; Alabeo=Matematica.Clamp(grados,0,180); AutoAlabeo=false; }
        public void AlternarAutoAlabeo() { if(Resultado==null) AutoAlabeo=!AutoAlabeo; }
        public void AlternarAutoParacaidas() { if(Resultado==null) AutoParacaidas=!AutoParacaidas; }
        public bool Reingresar()
        {
            if(!ListaReentrada || Resultado!=null) return false;
            if(!Capsula.ServicioSeparado) { Emitir("Separa el módulo de servicio antes del reingreso.",true); return false; }
            double gamma=AnguloEntrada+CorreccionEntrada;
            if(gamma<(double)Datos.Fisica["limites"]["corredor_reingreso_gamma_deg"][0] || gamma>(double)Datos.Fisica["limites"]["corredor_reingreso_gamma_deg"][1])
            { Terminar(TipoResultado.PerdidaTotal,"Entrada fuera del corredor −6.5° a −5.5°."); return false; }
            if(FalloElectrico) { Emitir("Recupera el suministro eléctrico antes del reingreso.",true); return false; }
            ListaReentrada=false; reingreso=new DinamicaReingreso(Datos,Masa); inicioReingreso=Tiempo;
            EstadoReingreso=new[] {(double)Datos.Fisica["capsula"]["altura_interfaz_m"],(double)Datos.Fisica["capsula"]["v_entrada_retorno_lunar_ms"],Matematica.Rad(gamma)};
            Cambiar(Fase.Regreso); Emitir("Reingreso · controla alabeo, calentamiento y fuerza G."); return true;
        }
        internal void AvanzarReingreso(double dt)
        {
            reingreso.Sigma=AutoAlabeo ? AlabeoAutomatico() : Alabeo;
            Alabeo=reingreso.Sigma;
            EstadoReingreso=IntegradorRK4.Paso(reingreso,Tiempo,EstadoReingreso,dt); Tiempo+=dt;
            reingreso.Derivada(Tiempo,EstadoReingreso);
            Altura=Math.Max(0,EstadoReingreso[0]); Velocidad=EstadoReingreso[1]; Q=reingreso.Q; FuerzaG=reingreso.FuerzaG; Calor=reingreso.Calor;
            MaxG=Math.Max(MaxG,FuerzaG);
            if(FuerzaG>(double)Datos.Fisica["limites"]["g_max_reingreso"]) { Terminar(TipoResultado.PerdidaTotal,"Reingreso: límite de 4.5 G excedido."); return; }
            if(Calor>(double)Datos.Juego["calor_limite_wm2"][Nave.CapaId]*Capsula.IntegridadEscudo) { Terminar(TipoResultado.PerdidaTotal,"Escudo térmico sobrepasado."); return; }
            if(Altura<= (double)Datos.Fisica["paracaidas"][0]["h_max"])
            {
                descenso=new DinamicaDescenso(Datos,Masa); EstadoDescenso=new[] {Altura,-Math.Max((double)Datos.Juego["velocidad_descenso_inicial_ms"],Math.Abs(Velocidad*Math.Sin(EstadoReingreso[2])))};
                Cambiar(Fase.Aterrizaje); Emitir("Descenso · prepara paracaídas de frenado y principales.");
            }
            else if(Tiempo-inicioReingreso>7200) Terminar(TipoResultado.PerdidaTotal,"Rebote atmosférico: ventana de descenso perdida.");
        }
        double AlabeoAutomatico()
        {
            var cfg=Datos.Juego["guiado_reentrada"];
            double h=EstadoReingreso[0], v=Math.Max(1,EstadoReingreso[1]), gamma=EstadoReingreso[2];
            // Entrada con rebote: sustentación máxima inicialmente; luego controla altura de densidad.
            if(v>(double)cfg["velocidad_skip_ms"]) return gamma>0 && h>(double)cfg["altura_skip_m"] ? 180 : 0;
            double r=(double)Datos.Fisica["R_tierra"]+h, g=(double)Datos.Fisica["mu_tierra"]/(r*r);
            var caps=Datos.Fisica["capsula"]; double area=Math.PI*Math.Pow((double)caps["diametro_m"]/2,2), cd=(double)caps["Cd_reingreso"], ld=(double)caps["L_D"];
            double drag=0.5*new Atmosfera(Datos).Evaluar(h).Densidad*v*v*area*cd, lift=drag*ld;
            double rho=(double)cfg["g_objetivo"]*Masa*(double)Datos.Fisica["g0"]/(0.5*v*v*area*cd*Math.Sqrt(1+ld*ld));
            double objetivo=25000-6800*Math.Log(rho/0.04008);
            double trayectoria=Matematica.Clamp((objetivo-h)/(v*(double)cfg["horizonte_s"])-13600*drag/(Masa*v*v),Matematica.Rad(-80),Matematica.Rad(5));
            double cosSigma=((trayectoria-gamma)/(double)cfg["respuesta_s"]-(v/r-g/v)*Math.Cos(gamma))*Masa*v/Math.Max(1,lift);
            return Math.Acos(Matematica.Clamp(cosSigma,-1,1))*180/Math.PI;
        }
        public void DesplegarDrogues()
        {
            if(Fase!=Fase.Aterrizaje || Resultado!=null || Drogues || Altura>(double)Datos.Fisica["paracaidas"][1]["h_max"]) return;
            descenso.Drogues=true; Emitir("Dos paracaídas de frenado desplegados.");
        }
        public void DesplegarPrincipales()
        {
            if(Fase!=Fase.Aterrizaje || Resultado!=null || Principales || !Drogues || Altura>(double)Datos.Fisica["paracaidas"][2]["h_max"]) return;
            descenso.Principales=true; Emitir("Tres paracaídas principales desplegados · amerizaje en el Pacífico.");
        }
        internal void AvanzarDescenso(double dt)
        {
            if(AutoParacaidas) { DesplegarDrogues(); DesplegarPrincipales(); }
            EstadoDescenso=descenso.Paso(Tiempo,EstadoDescenso,dt); Tiempo+=dt;
            Altura=Math.Max(0,EstadoDescenso[0]); Velocidad=Math.Abs(EstadoDescenso[1]); Calor=0; FuerzaG=1;
            if(Altura<=0)
            {
                double min=(double)Datos.Fisica["limites"]["velocidad_amerizaje_ms"][0], max=(double)Datos.Fisica["limites"]["velocidad_amerizaje_ms"][1];
                Terminar(Principales && Velocidad>=min && Velocidad<=max ? TipoResultado.Exito : TipoResultado.PerdidaTotal,
                    "Amerizaje a "+Velocidad.ToString("0.00")+" m/s · rango seguro "+min+"–"+max+" m/s.");
            }
        }
        public void Abortar(string causa="Aborto solicitado por control de tierra.")
        {
            if(Resultado!=null) return;
            string rescate=Fase==Fase.Abordaje?"Lanzamiento cancelado; tripulación en tierra.":!LasSeparado?"Rescate con torre LAS.":"Retorno directo / aterrizaje alterno.";
            Terminar(TipoResultado.AbortoSeguro,causa+" "+rescate);
        }
        internal void Terminar(TipoResultado tipo,string causa)
        {
            if(Resultado!=null) return;
            Resultado=new ResultadoMision { Tipo=tipo,Causa=causa,Fase=Fase,Duracion=Tiempo,VelocidadAmerizaje=Fase==Fase.Aterrizaje?Velocidad:0,
                PerdidaMusd=Fase==Fase.Abordaje || tipo==TipoResultado.Exito ? 0 : Evaluacion.Costo*(tipo==TipoResultado.PerdidaTotal?1:(double)Datos.Riesgos["perdida_aborto_fraccion"]) };
            DecisionMotor=ListaTli=ListaReentrada=false; Emitir(tipo+" · "+causa,tipo!=TipoResultado.Exito);
            if(tipo!=TipoResultado.Exito) OnMisionAbortada?.Invoke(Resultado);
        }
        void Cambiar(Fase fase) { Maquina.Cambiar(fase); OnFaseCambiada?.Invoke(fase); }
        internal void Emitir(string texto,bool alerta=false)
        {
            Mensaje=texto; var e=new EventoMision { Tiempo=Tiempo,Texto=texto,Alerta=alerta }; Bitacora.Add(e);
            if(Bitacora.Count>100) Bitacora.RemoveAt(0); OnEvento?.Invoke(e);
        }
    }
}
