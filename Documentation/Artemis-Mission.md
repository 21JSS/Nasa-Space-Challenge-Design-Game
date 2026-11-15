# AURORA / Artemis — lógica, controles y validación

La implementación interpreta el documento `Prompt_Space_Game_Unity_CSharp (1).md` como especificación de datos y mecánicas. La solicitud del usuario amplía su alcance con interfaz y gráficos. No se añadieron servicios remotos: el backend de la simulación es C# local, independiente de Unity.

## Ejecutar

Abrir `Assets/Scenes/LaunchDemo.unity` y pulsar Play. La escena genera su mundo y su Canvas. No es necesario colocar manualmente componentes nuevos. Los valores iniciales se generan con semilla 2026; puede cambiarse en la interfaz. Si el presupuesto no alcanza para el perfil seleccionado, elegir una alternativa o generar otra misión.

1. Elegir uno de los ocho perfiles, o recorrer estructura, combustible y escudo por separado. Las combinaciones no incluidas en la matriz no pueden lanzar.
2. Ajustar autonomía, abordar cuatro astronautas y resolver el posible aplazamiento por lluvia. El modo **Turista** usa un crucero guiado; **Laboratorio** propaga la trayectoria gravitatoria sin garantía de retorno.
3. Iniciar el lanzamiento. SRB, LAS y núcleo se separan automáticamente con sus hitos y condiciones de seguridad. Un engine-out detiene el reloj y presenta Continuar / Abortar.
4. Confirmar TLI. Durante el recorrido pueden ejecutarse TCM positivas o negativas, activar ahorro eléctrico y preparar el ángulo de entrada. El ICPS se libera desde T+12,240 s.
5. En la ventana de regreso, separar el módulo de servicio. Queda la cabina de los astronautas con su escudo. Confirmar reingreso dentro del corredor de −6.5° a −5.5°.
6. Usar alabeo automático o mover el control manual. Los paracaídas también tienen modo automático y botones manuales con interbloqueos de altura.
7. Consultar el resultado, la pérdida económica y la bitácora. Repetir conserva la semilla; Nueva semilla genera otra misión.

Controles permanentes: cámara general/cabina, audio, pausa, aceleración ×1/×5/×20, aborto y reinicio. Atajos: Espacio para lanzar, F para abordar y R para reiniciar. El panel **Perfil y bitácora** contiene descripción, riesgos, costos y eventos. El factor temporal mostrado incluye la compresión propia de cada fase; la física utiliza pasos independientes del framerate.

## Organización

- `Assets/LaunchPrototype/Domain/`: ensamblado `Artemis.Domain`, sin referencias al motor. Datos, diseño, costos, validación, RNG inyectable, estados, riesgos, RK4 y modelos físicos.
- `Assets/LaunchPrototype/Resources/MissionData/`: las siete tablas JSON. Se preservan los registros y procedencias del documento; los ajustes nuevos están identificados como SUPUESTO en `fisica.juego` y `riesgos`.
- `LaunchDemo.cs` y `MissionPresentation.cs`: puente de Unity y visualización. El HUD consulta DTOs/propiedades y manda comandos a `Mision`; los eventos permiten conectar otra interfaz.
- `LaunchHud.cs`: uGUI/TMP con selección de perfiles, interbloqueos y controles de cada fase.
- `MisionDemoRunner.cs`: carga de Resources y simulación reproducible completa sin fallos aleatorios.
- `Tests/Editor/MissionTests.cs`: NUnit de las fórmulas y de los principales caminos de misión.
- `AgentScripts/ExtractMissionData.py`: extracción reproducible del MD original; depende de que siga disponible en Downloads.
- `AgentScripts/BakeUpperStages.cs`: recorte del Modelo 4 en tres mallas, conservando geometría y UV. Las mallas generadas están en `Resources/StageParts`; no se modifica el FBX original.

## Límites y decisiones de calibración

Esta es una simulación educativa jugable. No es una reproducción certificada de SLS/Artemis. Se distinguen deliberadamente estos puntos:

| Tema | Implementación y motivo |
|---|---|
| Costos | Fórmula modular del MD, incluyendo diferencia de costo de propelente del kerolox. Desde la misión 5, +290 M USD. Los ocho totales coinciden con la tabla. |
| Masas | Se calculan desde módulos, sin doble conteo. La tabla de referencia tiene redondeos de hasta 0.005 t; las pruebas toleran 0.011 t. |
| Δv | Se recalcula por etapas y consumo simultáneo SRB/núcleo. Baseline: 13,526.55 m/s, frente al aproximado de 13,240 m/s del MD. No se muestra el aproximado como resultado medido. |
| Atmósfera y rotación | La velocidad inercial inicial incluye la rotación terrestre. El arrastre descuenta la velocidad de la atmósfera corrotante; de otro modo se introduce viento aparente de unos 400 m/s en la plataforma. |
| MECO | Al mínimo de acelerador, la etapa ligera puede exceder 3.85 G antes del agotamiento. Si ya alcanzó velocidad orbital, el control hace un corte preventivo y separa el núcleo con su remanente. Si no, siguen aplicando las reglas de aborto. |
| TLI | Aproximación impulsiva con consumo calculado por Tsiolkovsky y cambio vectorial desde la velocidad de salida del núcleo. No se simula todavía el encendido de duración finita del RL10 ni la espera en órbita de aparcamiento. |
| Crucero Turista | Referencia guiada comprimida de ida y retorno lunar de ocho días; la altitud y la velocidad visual de misión se obtienen de esa referencia. También se integra un vector gravitatorio Tierra/Luna, pero la referencia guiada no es una solución balística CR3BP. Las TCM consumen reserva RCS y modifican el vector y el ajuste de entrada. |
| Crucero Laboratorio | RK4 con Tierra y Luna circular, incluyendo aceleración indirecta geocéntrica. No incorpora un solucionador de tiro de retorno libre; puede requerir correcciones o acabar fuera de ventana. |
| Masa de reentrada | El MD mantiene 26.7 t de Orión + servicio, pero con su CdA esa masa ameriza a ~13.6 m/s. Tras separar servicio se usa el supuesto configurable de 8.5 t de cabina + TPS. Con Avcoat, 9.35 t alcanzan ~8.03 m/s. |
| Reentrada | RK4 de altura, rapidez y ángulo; Sutton–Graves, sustentación, alabeo y límite 4.5 G. El alabeo automático incluye un rebote atmosférico y control de densidad/energía. El corredor de entrada se comprueba en la interfaz de 120 km, no durante todo el descenso. |
| Paracaídas | Arrastre por fases. La apertura instantánea produce rigidez numérica; se usan subpasos RK4 adaptativos durante el descenso. El modelo no resuelve el inflado progresivo ni las cargas de apertura de las telas. |
| Abortos | LAS antes de su eyección; después se informa retorno directo/alterno. El aborto seguro se resuelve como resultado de juego; todavía no se integra una trayectoria completa de rescate. |
| Pérdidas | Vehículo desechable: vuelo fallido pierde la inversión según el factor configurable; cancelación en tierra no cuenta como pérdida. En éxito se muestra costo de misión y pérdida cero. |

### Referencias que no cumplen aún la tolerancia de ±5 %

No se ha falseado la telemetría para hacerla coincidir con los hitos de la especificación. En la corrida baseline, con latitud/inclinación 28.627°:

| Tiempo | Altura del MD | Resultado de ecuaciones |
|---|---:|---:|
| 7 s | 170 m | 106.5 m |
| 70 s | 12,970 m | 10,850.7 m |
| 129 s | 47,500 m | 39,811.2 m |
| ~198 s | 87,000 m | 86,282.2 m |

M0 = 2,610,000 kg; TWR = 1.52762; Max-Q = 25,180 Pa. El MECO preventivo ocurre cerca de 457 s, por lo que los hitos de núcleo a 495 s e ICPS a 22,279 km no están calibrados a las altitudes del MD. La separación ICPS conserva el umbral temporal, con resolución del paso orbital de 30 s. El modo Turista permite terminar la misión sin declarar que esas discrepancias físicas están resueltas.

## Validación realizada

Unity 6000.6.9f1 / URP 17.6.0. Compilación completada sin errores. NUnit: **25 pruebas, 25 aprobadas, 0 fallos**. Incluyen los ocho perfiles de costos y masas, rotación, semillas/rangos, presupuesto, tripulación, lluvia, fallos SRB y motores, pausa por decisión, límites, RK4 contra solución analítica, independencia del framerate y misión Turista completa con todas las separaciones y amerizaje exitoso.

La prueba de misión completa usa un RNG sin fallos aleatorios para comprobar la ruta nominal. Las pruebas de riesgos usan RNG determinista forzado. Una partida normal conserva los riesgos y puede fallar.

Se inició Play Mode para la revisión visual, pero la solicitud de capturas/inspección de la ejecución fue rechazada. **La inspección visual y la prueba de clics en pantalla quedan sin verificar**; no se asigna una puntuación visual sin capturas. La validación anterior acredita compilación y dominio, no la apariencia del render.

## Restauración y fuentes técnicas

`Documentation/ArtemisRestore.zip` contiene los dos scripts originales tal como estaban al empezar, incluyendo los cambios previos del usuario. No se ha creado un commit ni se han descartado sus archivos. La prueba dejó el Editor en Play Mode y auto-tick habilitado; el Editor también estaba en Play Mode al iniciar el trabajo.

Se consultaron las habilidades `unity-pipeline`, `ui-ugui` y `craft-game-floors`; referencias `vfx.md` y `visual-scorecard.md`; documentación local de Pipeline `commands/scripts.md`, `commands/capture.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md` y `commands/runtime.md`. El campo de estrellas reutiliza su buffer de partículas mediante [ParticleSystem.SetParticles](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.SetParticles.html).
