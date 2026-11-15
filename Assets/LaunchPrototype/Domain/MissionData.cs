using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Artemis
{
    // Dominio independiente de Unity. La fuente de texto se inyecta (Resources, disco o pruebas).
    public sealed class RepositorioDatos
    {
        public Dictionary<string, MaterialCohete> Materiales { get; }
        public Dictionary<string, CapaTermica> Capas { get; }
        public Dictionary<string, Combustible> Combustibles { get; }
        public CombinacionCohete[] Combinaciones { get; }
        public JObject Presupuesto { get; }
        public JObject Fisica { get; }
        public JObject Riesgos { get; }
        public JObject Juego => (JObject)Fisica["juego"];
        public RepositorioDatos(Func<string, string> leer)
        {
            Materiales = JsonConvert.DeserializeObject<Dictionary<string, MaterialCohete>>(leer("materiales"));
            Capas = JsonConvert.DeserializeObject<Dictionary<string, CapaTermica>>(leer("capas_termicas"));
            Combustibles = JsonConvert.DeserializeObject<Dictionary<string, Combustible>>(leer("combustibles"));
            Combinaciones = JsonConvert.DeserializeObject<CombinacionCohete[]>(leer("combinaciones"));
            Presupuesto = JObject.Parse(leer("presupuesto"));
            Fisica = JObject.Parse(leer("fisica"));
            Riesgos = JObject.Parse(leer("riesgos"));
            if (Combinaciones.Length != 8 || Materiales.Count == 0 || Capas.Count == 0 || Combustibles.Count == 0)
                throw new InvalidOperationException("Tablas de misión incompletas.");
            foreach (var c in Combinaciones)
                if (!Materiales.ContainsKey(c.material) || !Capas.ContainsKey(c.capa_termica) || !Combustibles.ContainsKey(c.combustible))
                    throw new InvalidOperationException("Referencias inválidas en perfil " + c.id);
            if (N(Juego, "dt_s") <= 0 || N(Fisica, "g0") <= 0) throw new InvalidOperationException("Paso o gravedad inválidos.");
        }
        public static double N(JToken token, string key) => token[key]?.Value<double>() ?? throw new InvalidOperationException("Falta parámetro: " + key);
        public CombinacionCohete Buscar(string material, string combustible, string capa) =>
            Combinaciones.FirstOrDefault(c => c.material == material && c.combustible == combustible && c.capa_termica == capa);
    }
    public sealed class MaterialCohete { public string nombre; public double factor, costo_estructura_musd; public double[] precio_usd_t; }
    public sealed class CapaTermica { public string nombre; public double masa_t, costo_musd; }
    public sealed class Combustible
    {
        public string nombre;
        public double prop_etapa_central_t, prop_srb_t, prop_icps_t, prop_total_t, factor_isp_etapa_central;
        public double costo_propelentes_musd, ajuste_costo_etapa_central_musd;
    }
    public sealed class CombinacionCohete
    {
        public int id; public string perfil, descripcion, material, combustible, capa_termica;
        public double masa_seca_total_Mf_t, masa_cargada_M0_t, costo_total_estimado_musd;
    }
    public interface IRandom { double Next(); }
    public sealed class RandomSemilla : IRandom
    {
        readonly Random random;
        public RandomSemilla(int seed) { random = new Random(seed); }
        public double Next() => random.NextDouble();
    }
    public sealed class ParametrosIniciales
    {
        public int Semilla, NumeroMision = 1, Tripulacion;
        public double Latitud, Inclinacion, Presupuesto, AutonomiaDias;
        public string Clima;
        public double Azimut => Math.Asin(Math.Min(1, Math.Cos(Inclinacion * Math.PI / 180) / Math.Cos(Latitud * Math.PI / 180))) * 180 / Math.PI;
    }
    public sealed class GeneradorParametros
    {
        readonly RepositorioDatos d;
        public GeneradorParametros(RepositorioDatos datos) { d = datos; }
        public ParametrosIniciales Generar(int seed, int numeroMision = 1)
        {
            var r = new RandomSemilla(seed);
            var clima = new[] { "Eclipse", "Soleado", "Lluvioso" };
            double lat = RepositorioDatos.N(d.Juego,"latitud_min_deg") + r.Next() * (RepositorioDatos.N(d.Juego,"latitud_max_deg") - RepositorioDatos.N(d.Juego,"latitud_min_deg"));
            return new ParametrosIniciales { Semilla=seed, NumeroMision=Math.Max(1,numeroMision), Latitud=lat, Inclinacion=lat + r.Next()*(90-lat),
                Presupuesto=(double)d.Presupuesto["rango_aleatorio_musd"][0]+r.Next()*((double)d.Presupuesto["rango_aleatorio_musd"][1]-(double)d.Presupuesto["rango_aleatorio_musd"][0]),
                Clima=clima[(int)(r.Next()*clima.Length)], Tripulacion=(int)d.Juego["tripulacion"], AutonomiaDias=(double)d.Juego["autonomia_inicial_dias"] };
        }
    }
    public sealed class EtapaCohete
    {
        public string Nombre { get; }
        public double MasaSeca { get; }
        public double Propelente { get; internal set; }
        public bool Separada { get; internal set; }
        public EtapaCohete(string nombre, double seca, double propelente) { Nombre=nombre; MasaSeca=seca; Propelente=propelente; }
        public double Masa => Separada ? 0 : MasaSeca + Math.Max(0, Propelente);
    }
    public sealed class CapsulaOrion
    {
        public double IntegridadEscudo { get; internal set; } = 1;
        public double ReservaRcs { get; internal set; }
        public bool ServicioSeparado { get; internal set; }
    }
    public sealed class Cohete
    {
        public string MaterialId { get; } public string CombustibleId { get; } public string CapaId { get; }
        public MaterialCohete Material { get; } public Combustible Combustible { get; } public CapaTermica Capa { get; }
        public Cohete(RepositorioDatos d, string material, string combustible, string capa)
        { MaterialId=material; CombustibleId=combustible; CapaId=capa; Material=d.Materiales[material]; Combustible=d.Combustibles[combustible]; Capa=d.Capas[capa]; }
    }
    public sealed class CalculadoraCostos
    {
        readonly RepositorioDatos d;
        public CalculadoraCostos(RepositorioDatos datos) { d=datos; }
        public double Calcular(Cohete c, int numeroMision)
        {
            var b=d.Presupuesto;
            return RepositorioDatos.N(b,"base_modelo_musd")-RepositorioDatos.N(b,"estructura_base_musd")-RepositorioDatos.N(b,"escudo_base_musd")
                +c.Material.costo_estructura_musd+c.Capa.costo_musd+c.Combustible.ajuste_costo_etapa_central_musd
                +c.Combustible.costo_propelentes_musd-RepositorioDatos.N(b,"combustible_base_musd")
                +(numeroMision >= (int)b["primera_mision_rs25"] ? (double)b["recargo_rs25_musd"] : 0);
        }
    }
    public sealed class EvaluacionCohete
    {
        public Cohete Cohete; public CombinacionCohete Perfil;
        public double MasaSeca, MasaCargada, DeltaV, Twr, Costo, RelacionMasa;
        public readonly List<string> Bloqueos = new List<string>();
        public readonly List<string> Riesgos = new List<string>();
        public bool Viable => Bloqueos.Count == 0;
    }
    public interface IReglaValidacion { string Validar(EvaluacionCohete e, ParametrosIniciales p); }
    public sealed class ReglaValidacion : IReglaValidacion
    {
        readonly Func<EvaluacionCohete,ParametrosIniciales,string> regla;
        public ReglaValidacion(Func<EvaluacionCohete,ParametrosIniciales,string> r) { regla=r; }
        public string Validar(EvaluacionCohete e, ParametrosIniciales p) => regla(e,p);
    }
    public sealed class ValidadorMision
    {
        readonly List<IReglaValidacion> reglas = new List<IReglaValidacion>();
        public ValidadorMision(RepositorioDatos d)
        {
            reglas.Add(new ReglaValidacion((e,p)=>e.Perfil==null ? "Combinación fuera de los 8 perfiles certificados." : null));
            reglas.Add(new ReglaValidacion((e,p)=>e.Costo>p.Presupuesto ? "Presupuesto excedido por "+(e.Costo-p.Presupuesto).ToString("N1")+" M USD." : null));
            reglas.Add(new ReglaValidacion((e,p)=>e.Twr<(double)d.Juego["twr_min"] ? "TWR insuficiente para despegar." : null));
            reglas.Add(new ReglaValidacion((e,p)=>e.DeltaV<(double)d.Juego["dv_requerido_ms"]*(1-(double)d.Juego["margen_dv"]) ? "Delta-v insuficiente para órbita y TLI." : null));
            reglas.Add(new ReglaValidacion((e,p)=>p.Tripulacion!=(int)d.Juego["tripulacion"] ? "Deben abordar 4 astronautas." : null));
            reglas.Add(new ReglaValidacion((e,p)=>p.AutonomiaDias<(double)d.Juego["autonomia_min_dias"] || p.AutonomiaDias>(double)d.Juego["autonomia_max_dias"] ? "Autonomía requerida: 8–21 días." : null));
            reglas.Add(new ReglaValidacion((e,p)=>p.Inclinacion<Math.Abs(p.Latitud) || p.Inclinacion>90 ? "Inclinación incompatible con la latitud." : null));
        }
        public IEnumerable<string> Validar(EvaluacionCohete e, ParametrosIniciales p) => reglas.Select(r=>r.Validar(e,p)).Where(s=>s!=null);
        public static bool FueraLimite(double valor, double limite) => valor > limite;
    }
    public sealed class EvaluadorCohete
    {
        readonly RepositorioDatos d;
        public EvaluadorCohete(RepositorioDatos datos) { d=datos; }
        public EvaluacionCohete Evaluar(Cohete c, ParametrosIniciales p)
        {
            double k=c.Material.factor, srb=(double)d.Fisica["masas"]["srb_kg"], core=(double)d.Fisica["masas"]["core_kg"]*k;
            double upper=(double)d.Fisica["masas"]["icps_kg"]*k, las=(double)d.Fisica["masas"]["las_kg"];
            double orion=(double)d.Fisica["masas"]["orion_esm_kg"]+c.Capa.masa_t*1000;
            double dry=srb+core+upper+las+orion, prop=c.Combustible.prop_total_t*1000, g=(double)d.Fisica["g0"];
            // Tsiolkovsky por etapas: el núcleo consume combustible durante los SRB.
            double coreFlow=(double)d.Fisica["motores"]["RS25x4"]["empuje_100_N"]/(g*(double)d.Fisica["motores"]["RS25x4"]["isp_nivel_mar"]);
            double earlyCore=Math.Min(c.Combustible.prop_etapa_central_t*1000,coreFlow*(double)d.Fisica["motores"]["SRB"]["t_separacion_s"]);
            double pb=c.Combustible.prop_srb_t*1000, pc=c.Combustible.prop_etapa_central_t*1000, pi=c.Combustible.prop_icps_t*1000;
            double ispCore=(double)d.Fisica["motores"]["RS25x4"]["isp_vacio"]*c.Combustible.factor_isp_etapa_central;
            double ispMix=(pb*(double)d.Fisica["motores"]["SRB"]["isp_vacio"]+earlyCore*ispCore)/(pb+earlyCore);
            double afterBoost=dry+prop-pb-earlyCore;
            double dv=ispMix*g*Math.Log((dry+prop)/afterBoost);
            dv+=ispCore*g*Math.Log((afterBoost-srb-las)/(core+upper+orion+pi));
            dv+=(double)d.Fisica["motores"]["ICPS_RL10C2"]["isp"]*g*Math.Log((upper+orion+pi)/(upper+orion));
            var e=new EvaluacionCohete { Cohete=c, Perfil=d.Buscar(c.MaterialId,c.CombustibleId,c.CapaId), MasaSeca=dry, MasaCargada=dry+prop,
                RelacionMasa=(dry+prop)/dry, DeltaV=dv, Twr=(double)d.Juego["empuje_evaluacion_N"]/((dry+prop)*g), Costo=new CalculadoraCostos(d).Calcular(c,p.NumeroMision) };
            e.Bloqueos.AddRange(new ValidadorMision(d).Validar(e,p));
            if (c.CapaId=="Losetas") e.Riesgos.Add("Losetas: impactos y maniobras bruscas dañan el escudo.");
            if (c.MaterialId=="CFRP") e.Riesgos.Add("CFRP: agrietamiento durante Max-Q.");
            if (c.CapaId=="Corcho") e.Riesgos.Add("Corcho: menor tolerancia térmica lunar.");
            if (p.Clima=="Lluvioso") e.Riesgos.Add("Lluvia: viento y riesgo de ascenso ×1.5.");
            if (p.Clima=="Eclipse") e.Riesgos.Add("Eclipse: riesgo eléctrico ×1.3.");
            return e;
        }
    }
    public sealed class DisenadorNave
    {
        readonly RepositorioDatos d;
        public Cohete Cohete { get; private set; }
        public DisenadorNave(RepositorioDatos datos) { d=datos; var c=d.Combinaciones[0]; Seleccionar(c.material,c.combustible,c.capa_termica); }
        public void Seleccionar(string material,string combustible,string capa) { Cohete=new Cohete(d,material,combustible,capa); }
    }
}
