"""Extrae las tablas del documento del usuario; los ajustes nuevos están marcados."""
import json, re
from pathlib import Path

source = Path(r'C:\Users\josiy\Downloads\Prompt_Space_Game_Unity_CSharp (1).md').read_text(encoding='utf-8-sig')
blocks = [json.loads(x) for x in re.findall(r'```json\s*(.*?)```', source, re.S)]
out = Path('Assets/LaunchPrototype/Resources/MissionData')
out.mkdir(parents=True, exist_ok=True)
def write(name, value):
    (out / (name + '.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
for key, val in blocks[0].items():
    write(key, val)
budget = blocks[1]
budget.update(exponente_material=0.12, estructura_base_musd=1326, escudo_base_musd=85,
              combustible_base_musd=3.5, recargo_rs25_musd=290, primera_mision_rs25=5)
write('presupuesto', budget)
physics = blocks[2]
physics['clima'] = {
    'Soleado': dict(viento=1.0, riesgo_ascenso=1.0, aplazamiento=0.0, riesgo_electrico=1.0),
    'Lluvioso': dict(viento=1.5, riesgo_ascenso=1.5, aplazamiento=0.15, riesgo_electrico=1.0),
    'Eclipse': dict(viento=1.0, riesgo_ascenso=1.0, aplazamiento=0.0, riesgo_electrico=1.3)}
physics['masas'] = dict(srb_kg=197000, core_kg=85000, icps_kg=3500, orion_esm_kg=25850, las_kg=7300)
physics['juego'] = dict(
    origen='SUPUESTO: parámetros de jugabilidad; trayectoria asistida, no navegación certificada',
    dt_s=0.2, paso_orbital_s=30, latitud_min_deg=5, latitud_max_deg=55,
    tripulacion=4, autonomia_min_dias=8, autonomia_max_dias=21,
    autonomia_inicial_dias=14, duracion_mision_dias=8, twr_min=1.2,
    dv_requerido_ms=12500, margen_dv=0.07, empuje_evaluacion_N=39100000,
    densidad_amerizaje=1.158, masa_capsula_sin_escudo_kg=8500,
    origen_masa_capsula='SUPUESTO: separar ESM antes de reentrada; 8.5 t + TPS da ~8.0 m/s. 26.7 t con CdA original da ~13.6 m/s.',
    distancia_luna_m=384400000, radio_luna_m=1737400, periodo_lunar_s=2360591,
    orbita_aparcamiento_m=162500, tli_inicio_s=1200, tli_fin_s=2300,
    rcs_reserva_ms=80, correccion_tcm_ms=8, eventos_recorrido=4,
    calor_limite_wm2=dict(Avcoat=16000000, Losetas=14000000, Corcho=9000000),
    factores_q=dict(AlLi2195=1, Acero301=1.1, Al2024=0.95, Ti6Al4V=1.15, CFRP=0.9),
    destruccion_q_factor=1.6, g_tolerancia_numerica=0.03,
    asistencia=True, velocidad_tiempo_ascenso=8, velocidad_tiempo_crucero=7200,
    velocidad_tiempo_reingreso=120, velocidad_tiempo_descenso=12,
    guiado_reentrada=dict(velocidad_skip_ms=9000, altura_skip_m=60000, g_objetivo=2.6, horizonte_s=30, respuesta_s=15),
    origen_asistencia='SUPUESTO: piloto de navegación asiste inserción, retorno libre y alabeo; se puede desactivar para laboratorio de ecuaciones crudas.',
    cadencia_maxq_s=1, velocidad_descenso_inicial_ms=110,
    hitos=[dict(t=0,h=0,g=1.53),dict(t=7,h=170,g=1.46),dict(t=70,h=12970,g=1.54),dict(t=129,h=47500,g=1.15),dict(t=198,h=87000,g=0),dict(t=495,h=162500,g=0),dict(t=12240,h=22279000,g=0)])
write('fisica', physics)
write('riesgos', dict(origen='Documento: SRB 0.4%; otros valores SUPUESTO configurables',
    p_srb=0.004, p_motor_segundo=0.000001, motores=4, consumo_engine_out=1.05,
    p_impacto=0.015, p_agrietamiento_maxq=0.04, p_electrico=0.01,
    dano_escudo_impacto=0.25, dano_escudo_maniobra=0.10, perdida_aborto_fraccion=1.0))
print('Siete tablas JSON extraídas; ajustes documentados en fisica.juego y riesgos.')
