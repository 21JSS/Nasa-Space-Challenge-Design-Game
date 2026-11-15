using System;
using System.Linq;
using Artemis;
using NUnit.Framework;
using UnityEngine;

public sealed class MissionTests
{
    RepositorioDatos d;
    sealed class FixedRandom : IRandom { readonly double value; public FixedRandom(double v) { value=v; } public double Next()=>value; }
    [SetUp] public void Setup() { d=new RepositorioDatos(n=>Resources.Load<TextAsset>("MissionData/"+n).text); }
    Mision Mission(double rng=.99999)
    {
        var p=new GeneradorParametros(d).Generar(2026); p.Presupuesto=3876; p.Clima="Soleado"; p.Latitud=p.Inclinacion=28.627;
        return new Mision(d,p,new FixedRandom(rng));
    }
    [TestCase(1,3468,319.5,2610)]
    [TestCase(2,3486.6,319.1,2609.6)]
    [TestCase(3,3322.5,319.85,2778.35)]
    [TestCase(4,3201.5,350.48,2640.98)]
    [TestCase(5,3108.6,350.07,2808.57)]
    [TestCase(6,3254.5,332.77,2623.27)]
    [TestCase(7,3644.4,306.23,2596.73)]
    [TestCase(8,3820.8,292.55,2583.05)]
    public void TablasCostosYMasas(int id,double costo,double seca,double cargada)
    {
        var m=Mission(); var c=d.Combinaciones[id-1]; m.Seleccionar(c.material,c.combustible,c.capa_termica);
        Assert.That(m.Evaluacion.Costo,Is.EqualTo(costo).Within(.01));
        Assert.That(m.Evaluacion.MasaSeca/1000,Is.EqualTo(seca).Within(.011));
        Assert.That(m.Evaluacion.MasaCargada/1000,Is.EqualTo(cargada).Within(.011));
        Assert.That(m.Evaluacion.DeltaV,Is.GreaterThan(10000));
    }
    [Test] public void RotacionFlorida() { Assert.That(DinamicaOrbital.Rotacion(d,28.627),Is.EqualTo(407.7).Within(.3)); }
    [Test] public void PresupuestoBloqueaSinAbortar()
    { var m=Mission(); m.Parametros.Presupuesto=3060; m.Abordar(); Assert.That(m.Lanzar(),Is.False); Assert.That(m.Resultado,Is.Null); Assert.That(m.Fase,Is.EqualTo(Fase.Abordaje)); }
    [Test] public void RechazaCombinacionDesconocida()
    { var m=Mission(); m.Seleccionar("Ti6Al4V","RP1_LOX","Corcho"); Assert.That(m.Evaluacion.Perfil,Is.Null); m.Abordar(); Assert.That(m.Lanzar(),Is.False); }
    [Test] public void RecargoDeMotoresMisionCinco()
    { var m=Mission(); double costo=m.Evaluacion.Costo; m.Parametros.NumeroMision=5; m.Evaluar(); Assert.That(m.Evaluacion.Costo-costo,Is.EqualTo(290)); }
    [Test] public void SemillaReproducibleYRangos()
    {
        var g=new GeneradorParametros(d);
        for(int i=0;i<100;i++) { var p=g.Generar(i); var q=g.Generar(i); Assert.That(p.Presupuesto,Is.EqualTo(q.Presupuesto)); Assert.That(p.Presupuesto,Is.InRange(3060,3876)); Assert.That(p.Inclinacion,Is.InRange(Math.Abs(p.Latitud),90)); }
    }
    [Test] public void NoLanzaSinAbordar() { var m=Mission(); Assert.That(m.Lanzar(),Is.False); }
    [Test] public void LluviaPermiteEsperarOAceptar()
    { var m=Mission(0); m.Parametros.Clima="Lluvioso"; m.Abordar(); Assert.That(m.PrepararLanzamiento(),Is.False); Assert.That(m.Aplazamiento,Is.True); m.AceptarClima(); Assert.That(m.PrepararLanzamiento(),Is.True); }
    [Test] public void SrbCatastroficoPierdeTodaLaInversion()
    { var m=Mission(0); m.Abordar(); m.Lanzar(); m.Avanzar(.2); Assert.That(m.Resultado.Tipo,Is.EqualTo(TipoResultado.PerdidaTotal)); Assert.That(m.Resultado.PerdidaMusd,Is.EqualTo(m.Evaluacion.Costo)); }
    [Test] public void EngineOutPausaYRechazoUsaLas()
    {
        var m=Mission(); m.Abordar(); m.Lanzar(); m.Avanzar(1); m.FalloMotor(2); double t=m.Tiempo; m.Avanzar(100);
        Assert.That(m.Motores,Is.EqualTo(3)); Assert.That(m.Tiempo,Is.EqualTo(t)); m.Abortar(); Assert.That(m.Resultado.Tipo,Is.EqualTo(TipoResultado.AbortoSeguro)); Assert.That(m.Resultado.Causa,Does.Contain("LAS"));
    }
    [Test] public void FalloMultipleAborta() { var m=Mission(); m.Abordar(); m.Lanzar(); m.FalloMotor(1); m.FalloMotor(2); Assert.That(m.Resultado.Tipo,Is.EqualTo(TipoResultado.AbortoSeguro)); }
    [Test] public void LimitesGyQNoAceptanValoresSuperiores()
    { Assert.That(ValidadorMision.FueraLimite(33501,33500),Is.True); Assert.That(ValidadorMision.FueraLimite(4.51,4.5),Is.True); Assert.That(ValidadorMision.FueraLimite(3.85,3.85),Is.False); }
    [Test] public void MotorReduceEmpujeYConsumeMasPorMotorRestante()
    {
        var c=Mission().Nave; var p=new Propulsion(d); var a=p.Evaluar(50,101325,2000000,900000,900000,c,4); var b=p.Evaluar(50,101325,2000000,900000,900000,c,3);
        Assert.That(b.FuerzaCore/a.FuerzaCore,Is.EqualTo(.75).Within(.0001)); Assert.That(b.FlujoCore/a.FlujoCore,Is.EqualTo(.75*1.05).Within(.0001));
    }
    [Test] public void ProbabilidadAcumuladaCorrecta() { Assert.That(SistemaFallos.Acumulada(.000001,15,180),Is.EqualTo(.00269636).Within(.0000001)); }
    sealed class Exponencial : IModeloFisico { public double[] Derivada(double t,double[] y)=>new[]{y[0]}; }
    [Test] public void Rk4ContraSolucionAnalitica()
    { var y=new[]{1.0}; for(int i=0;i<10;i++) y=IntegradorRK4.Paso(new Exponencial(),i*.1,y,.1); Assert.That(y[0],Is.EqualTo(Math.E).Within(.000003)); }
    [Test] public void CapsulaSeparadaAlcanzaVelocidadSegura()
    { var p=new DinamicaDescenso(d,9350); Assert.That(p.Terminal,Is.InRange(7.9,8.2)); }
    [Test] public void PasoTemporalIndependienteDelFrame()
    { var a=Mission(); var b=Mission(); a.Abordar(); b.Abordar(); a.Lanzar(); b.Lanzar(); for(int i=0;i<100;i++) a.Avanzar(.1); for(int i=0;i<10;i++) b.Avanzar(1); Assert.That(a.Altura,Is.EqualTo(b.Altura).Within(.001)); }
    [Test] public void MisionCompletaTurista()
    {
        var m=Mission(); m.Abordar(); Assert.That(m.Lanzar(),Is.True);
        for(int i=0;i<100000 && m.Resultado==null;i++) { if(m.ListaTli) m.InyectarTli(); if(m.ListaReentrada) { m.SepararServicio(); m.Reingresar(); } m.Avanzar(m.Paso); }
        Assert.That(m.Resultado,Is.Not.Null); Assert.That(m.Resultado.Tipo,Is.EqualTo(TipoResultado.Exito),m.Resultado.Causa);
        Assert.That(m.SrbSeparado && m.LasSeparado && m.CoreSeparado && m.IcpsSeparado && m.Capsula.ServicioSeparado,Is.True);
    }
}
