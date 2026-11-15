using System;
using Newtonsoft.Json.Linq;

namespace Artemis
{
    public static class Matematica
    {
        public static double Clamp(double x,double min,double max) => Math.Max(min,Math.Min(max,x));
        public static double Hipot(double x,double y) => Math.Sqrt(x*x+y*y);
        public static double Rad(double grados) => grados*Math.PI/180;
    }
    public struct Aire { public double Temperatura, Presion, Densidad, Sonido; }
    public sealed class Atmosfera
    {
        readonly JObject f;
        public Atmosfera(RepositorioDatos d) { f=d.Fisica; }
        public Aire Evaluar(double altura)
        {
            double h=Math.Max(0,altura), t, p, rho, gas=(double)f["R_aire"], g=(double)f["g0"];
            if (h<11000) { t=(double)f["sitio_base"]["T0"]-0.0065*h; p=(double)f["sitio_base"]["P0"]*Math.Pow(t/(double)f["sitio_base"]["T0"],5.2561); rho=p/(gas*t)*(1-0.015*Math.Exp(-h/2500)); }
            else if (h<25000) { t=216.65; p=22632*Math.Exp(-g*(h-11000)/(gas*t)); rho=p/(gas*t); }
            else { t=216.65; rho=0.04008*Math.Exp(-(h-25000)/6800); p=rho*gas*t; }
            return new Aire { Temperatura=t, Presion=p, Densidad=rho, Sonido=Math.Sqrt((double)f["gamma_aire"]*gas*t) };
        }
    }
    public static class Viento
    {
        public static double Evaluar(double h, double factor) => factor*(6.5*Math.Pow(Math.Max(h,1)/10,0.14)*Math.Exp(-Math.Max(h,0)/8000)+35*Math.Exp(-Math.Pow(h-12000,2)/(2*3000*3000)));
    }
    public static class Aerodinamica
    {
        public static double Cd(double mach)
        {
            if(mach<0.8) return 0.32;
            if(mach<1.15) return 0.32+1.4286*(mach-0.8);
            if(mach<2.5) return 0.82-0.3481*(mach-1.15);
            return Math.Max(0.24,0.35-0.03*(mach-2.5));
        }
        public static double PresionDinamica(double rho,double velocidad) => 0.5*rho*velocidad*velocidad;
    }
    public interface IModeloFisico { double[] Derivada(double tiempo, double[] estado); }
    public static class IntegradorRK4
    {
        public static double[] Paso(IModeloFisico modelo,double t,double[] y,double dt)
        {
            var a=modelo.Derivada(t,y); var aux=new double[y.Length];
            for(int i=0;i<y.Length;i++) aux[i]=y[i]+a[i]*dt/2;
            var b=modelo.Derivada(t+dt/2,aux);
            for(int i=0;i<y.Length;i++) aux[i]=y[i]+b[i]*dt/2;
            var c=modelo.Derivada(t+dt/2,aux);
            for(int i=0;i<y.Length;i++) aux[i]=y[i]+c[i]*dt;
            var e=modelo.Derivada(t+dt,aux); var result=new double[y.Length];
            for(int i=0;i<y.Length;i++) result[i]=y[i]+dt*(a[i]+2*b[i]+2*c[i]+e[i])/6;
            return result;
        }
    }
    public struct Empuje { public double FuerzaSrb, FuerzaCore, FlujoSrb, FlujoCore, Throttle; }
    public sealed class Propulsion
    {
        readonly RepositorioDatos d;
        public Propulsion(RepositorioDatos datos) { d=datos; }
        public Empuje Evaluar(double t,double presion,double masa,double propSrb,double propCore,Cohete cohete,int motores,double mando=1)
        {
            var s=d.Fisica["motores"]["SRB"]; var c=d.Fisica["motores"]["RS25x4"];
            double g=(double)d.Fisica["g0"], ratio=Matematica.Clamp(presion/(double)d.Fisica["sitio_base"]["P0"],0,1);
            double tau=Matematica.Clamp(t/(double)s["t_separacion_s"],0,1);
            double mdot=propSrb>0 && t<(double)s["t_separacion_s"] ? (double)s["mdot_base"]*(1.15-0.32*Math.Pow(Math.Sin(Math.PI*tau),1.4)-0.15*Math.Pow(tau,4)) : 0;
            double ispS=(double)s["isp_vacio"]-((double)s["isp_vacio"]-(double)s["isp_nivel_mar"])*ratio;
            double isp=((double)c["isp_vacio"]-((double)c["isp_vacio"]-(double)c["isp_nivel_mar"])*ratio)*cohete.Combustible.factor_isp_etapa_central;
            double forceBase=(double)c["empuje_100_N"]*isp/(double)c["isp_nivel_mar"]*motores/(int)d.Riesgos["motores"];
            double throttle=t>=(double)c["ventana_maxq_s"][0] && t<=(double)c["ventana_maxq_s"][1] ? (double)c["throttle_maxq"] : Matematica.Clamp(((double)d.Fisica["limites"]["g_max_ascenso"]*g*masa-mdot*g*ispS)/Math.Max(1,forceBase),(double)c["throttle_min"],(double)c["throttle_max"]);
            throttle*=Matematica.Clamp(mando,0,1);
            double force=propCore>0 ? forceBase*throttle : 0;
            return new Empuje { FuerzaSrb=mdot*g*ispS, FuerzaCore=force, FlujoSrb=mdot, FlujoCore=force/(g*isp)*(motores<(int)d.Riesgos["motores"] ? (double)d.Riesgos["consumo_engine_out"] : 1), Throttle=throttle };
        }
    }
    public sealed class DinamicaAscenso : IModeloFisico
    {
        readonly RepositorioDatos d; readonly Cohete c; readonly Atmosfera atmosfera; readonly Propulsion propulsion;
        public int Motores; public double Mando=1; public string Clima;
        public bool SrbSeparado, LasSeparado, CoreSeparado, IcpsSeparado;
        public double Q, FuerzaG, Mach, Empuje, Throttle;
        public DinamicaAscenso(RepositorioDatos datos,Cohete cohete,string clima)
        { d=datos; c=cohete; Clima=clima; atmosfera=new Atmosfera(d); propulsion=new Propulsion(d); Motores=(int)d.Riesgos["motores"]; }
        public double Masa(double[] y)
        {
            var m=d.Fisica["masas"];
            return (double)m["orion_esm_kg"]+c.Capa.masa_t*1000+(LasSeparado?0:(double)m["las_kg"])
                +(SrbSeparado?0:(double)m["srb_kg"]+Math.Max(0,y[4]))
                +(CoreSeparado?0:(double)m["core_kg"]*c.Material.factor+Math.Max(0,y[5]))
                +(IcpsSeparado?0:(double)m["icps_kg"]*c.Material.factor+Math.Max(0,y[6]));
        }
        public double[] Inicial(double rotacion) => new[] { 0.0,0,rotacion,0,c.Combustible.prop_srb_t*1000,c.Combustible.prop_etapa_central_t*1000,c.Combustible.prop_icps_t*1000 };
        public double[] Derivada(double t,double[] y)
        {
            var air=atmosfera.Evaluar(y[1]); double masa=Masa(y), g0=(double)d.Fisica["g0"], r=(double)d.Fisica["R_tierra"]+Math.Max(0,y[1]);
            // vx es inercial: la atmósfera corrota con la Tierra. Evita arrastre falso de ~400 m/s en la plataforma.
            double rot=(double)d.Fisica["omega_tierra"]*(double)d.Fisica["R_tierra"]*Math.Cos(Matematica.Rad(Inclinacion));
            double vx=y[2]-rot-Viento.Evaluar(y[1],(double)d.Fisica["clima"][Clima]["viento"]), vz=y[3], speed=Matematica.Hipot(vx,vz);
            double theta=t<7 ? 90 : t<129 ? 88.5-51.5*Math.Pow((t-7)/122,0.88) : t<485 ? 37-36.2*Math.Pow((t-129)/356,0.68) : 0.8;
            double angle=Matematica.Rad(theta), gamma=speed>1?Math.Atan2(vz,vx):angle;
            var e=propulsion.Evaluar(t,air.Presion,masa,SrbSeparado?0:y[4],CoreSeparado?0:y[5],c,Motores,Mando);
            double q=Aerodinamica.PresionDinamica(air.Densidad,speed), area=(double)d.Fisica["motores"][SrbSeparado?(CoreSeparado?"ICPS_RL10C2":"RS25x4"):"SRB"]["area_ref_m2"];
            double drag=q*area*Aerodinamica.Cd(speed/air.Sonido), thrust=e.FuerzaSrb+e.FuerzaCore;
            double ax=(thrust*Math.Cos(angle)-drag*Math.Cos(gamma))/masa, az=(thrust*Math.Sin(angle)-drag*Math.Sin(gamma))/masa;
            Q=q; Mach=speed/air.Sonido; FuerzaG=Matematica.Hipot(ax,az)/g0; Empuje=thrust; Throttle=e.Throttle;
            return new[] { y[2]*(double)d.Fisica["R_tierra"]/r, y[3], ax-y[2]*y[3]/r, az-(double)d.Fisica["mu_tierra"]/(r*r)+y[2]*y[2]/r, -e.FlujoSrb,-e.FlujoCore,0 };
        }
        public double Inclinacion;
        public void Paso(ref double[] y,double t,double dt)
        {
            y=IntegradorRK4.Paso(this,t,y,dt);
            for(int i=4;i<7;i++) y[i]=Math.Max(0,y[i]);
            Derivada(t+dt,y);
        }
    }
    public sealed class DinamicaOrbital : IModeloFisico
    {
        readonly RepositorioDatos d;
        public double FaseLuna, RcsX, RcsY;
        public DinamicaOrbital(RepositorioDatos datos) { d=datos; }
        public double[] Luna(double t)
        {
            double a=FaseLuna+2*Math.PI*t/(double)d.Juego["periodo_lunar_s"], r=(double)d.Juego["distancia_luna_m"];
            return new[] { r*Math.Cos(a),r*Math.Sin(a) };
        }
        public double[] Derivada(double t,double[] y)
        {
            var moon=Luna(t); double dx=y[0]-moon[0], dy=y[1]-moon[1];
            double r=Math.Max(1,Matematica.Hipot(y[0],y[1])), rm=Math.Max(1,Matematica.Hipot(dx,dy));
            double mu=(double)d.Fisica["mu_tierra"], ml=(double)d.Fisica["mu_luna"], lm=Matematica.Hipot(moon[0],moon[1]);
            // Término indirecto: origen geocéntrico acelerado por la Luna.
            return new[] { y[2],y[3],-mu*y[0]/(r*r*r)-ml*(dx/(rm*rm*rm)+moon[0]/(lm*lm*lm))+RcsX,
                -mu*y[1]/(r*r*r)-ml*(dy/(rm*rm*rm)+moon[1]/(lm*lm*lm))+RcsY };
        }
        public static double Circular(double mu,double radio) => Math.Sqrt(mu/radio);
        public static double Transferencia(double mu,double radio,double semieje) => Math.Sqrt(mu*(2/radio-1/semieje));
        public static double DeltaV(double isp,double g,double inicial,double final) => isp*g*Math.Log(inicial/final);
        public static double Rotacion(RepositorioDatos d,double inclinacion) => (double)d.Fisica["omega_tierra"]*(double)d.Fisica["R_tierra"]*Math.Cos(Matematica.Rad(inclinacion));
    }
    public sealed class DinamicaReingreso : IModeloFisico
    {
        readonly RepositorioDatos d; readonly Atmosfera atmosfera;
        public double Masa, Sigma, Calor, FuerzaG, Q;
        public DinamicaReingreso(RepositorioDatos datos,double masa) { d=datos; Masa=masa; atmosfera=new Atmosfera(d); }
        // Estado: altura, rapidez, ángulo de trayectoria (rad). Sigma es el alabeo, no gamma.
        public double[] Derivada(double t,double[] y)
        {
            var a=atmosfera.Evaluar(y[0]); var c=d.Fisica["capsula"]; double v=Math.Max(1,y[1]), r=(double)d.Fisica["R_tierra"]+Math.Max(0,y[0]);
            double g=(double)d.Fisica["mu_tierra"]/(r*r), area=Math.PI*Math.Pow((double)c["diametro_m"]/2,2);
            Q=0.5*a.Densidad*v*v; double drag=Q*area*(double)c["Cd_reingreso"], lift=drag*(double)c["L_D"];
            Calor=(double)c["k_sutton_graves"]*Math.Sqrt(a.Densidad/(double)c["radio_nariz_m"])*v*v*v;
            FuerzaG=Matematica.Hipot(drag,lift)/(Masa*(double)d.Fisica["g0"]);
            return new[] {v*Math.Sin(y[2]),-drag/Masa-g*Math.Sin(y[2]),lift*Math.Cos(Matematica.Rad(Sigma))/(Masa*v)+(v/r-g/v)*Math.Cos(y[2])};
        }
    }
    public sealed class DinamicaDescenso : IModeloFisico
    {
        readonly RepositorioDatos d; readonly Atmosfera aire;
        public double Masa; public bool Drogues, Principales;
        public DinamicaDescenso(RepositorioDatos datos,double masa) { d=datos; Masa=masa; aire=new Atmosfera(d); }
        public double Terminal => Math.Sqrt(2*Masa*(double)d.Fisica["g0"]/((double)d.Juego["densidad_amerizaje"]*(double)d.Fisica["paracaidas"][2]["CdA"]));
        public double[] Paso(double t,double[] y,double dt)
        {
            // La apertura de principales hace rígido el arrastre cuadrático. Subpasos mantienen estable RK4.
            double remaining=dt;
            while(remaining>1e-9)
            {
                double cda=(double)d.Fisica["paracaidas"][Principales?2:Drogues?1:0]["CdA"];
                double tau=Masa/(Math.Max(0.001,aire.Evaluar(y[0]).Densidad)*cda*Math.Max(1,Math.Abs(y[1])));
                double step=Math.Min(remaining,Math.Max(0.0001,tau*0.25));
                y=IntegradorRK4.Paso(this,t,y,step); remaining-=step; t+=step;
            }
            return y;
        }
        public double[] Derivada(double t,double[] y)
        {
            double r=(double)d.Fisica["R_tierra"]+Math.Max(0,y[0]);
            double cda=(double)d.Fisica["paracaidas"][Principales?2:Drogues?1:0]["CdA"];
            double rho=aire.Evaluar(y[0]).Densidad;
            // El modelo de amerizaje especifica 1.158 kg/m3; transición suave en los últimos 500 m.
            if (y[0]<500) rho+=(1-Matematica.Clamp(y[0]/500,0,1))*((double)d.Juego["densidad_amerizaje"]-rho);
            return new[] { y[1],-0.5*rho*y[1]*Math.Abs(y[1])*cda/Masa-(double)d.Fisica["mu_tierra"]/(r*r) };
        }
    }
}
