using System;
using System.Collections.Generic;
using System.Text;
using Artemis;
using UnityEngine;

/// <summary>Puente de datos Unity; las ecuaciones y decisiones viven en Artemis.Domain.</summary>
public static class MisionDemoRunner
{
    public static RepositorioDatos CargarDatos() => new RepositorioDatos(nombre =>
    {
        var text=Resources.Load<TextAsset>("MissionData/"+nombre);
        if(text==null) throw new InvalidOperationException("No se encontró MissionData/"+nombre);
        return text.text;
    });
    public sealed class SinFallos : IRandom { public double Next()=>0.999999; }
    public static string SimularBaseline()
    {
        var d=CargarDatos(); var p=new GeneradorParametros(d).Generar(2026);
        p.Presupuesto=3876; p.Clima="Soleado"; p.Latitud=p.Inclinacion=28.627;
        var m=new Mision(d,p,new SinFallos()); m.Abordar(); m.Lanzar();
        var report=new StringBuilder();
        report.AppendLine("Baseline: M0="+m.Evaluacion.MasaCargada+" kg; Δv="+m.Evaluacion.DeltaV+"; TWR="+m.Evaluacion.Twr);
        double[] hitos={7,70,129,198,495}; int h=0;
        for(int i=0;i<200000 && m.Resultado==null;i++)
        {
            if(m.ListaTli) m.InyectarTli();
            if(m.ListaReentrada) { m.SepararServicio(); m.Reingresar(); }
            m.Avanzar(m.Paso);
            if(h<hitos.Length && m.Tiempo>=hitos[h]) { report.AppendLine("T="+m.Tiempo.ToString("F1")+" H="+m.Altura.ToString("F1")+" G="+m.FuerzaG.ToString("F3")+" Q="+m.Q.ToString("F0")); h++; }
        }
        foreach(var e in m.Bitacora) report.AppendLine(e.ToString());
        report.AppendLine("Final: "+(m.Resultado?.Tipo.ToString()??"INCOMPLETA")+" h="+m.Altura+" v="+m.Velocidad+" t="+m.Tiempo+" MaxQ="+m.MaxQ+" MaxG="+m.MaxG);
        return report.ToString();
    }
}
