# AURORA — bienvenida y regreso a la Tierra

Implementado el 14 de noviembre de 2026 en Unity 6000.6.9f1 / URP.

- La cabina tiene una envolvente cerrada, ventanilla y escudo térmico. Durante la preparación de la reentrada anticipa el ángulo elegido; en vuelo sigue el ángulo calculado por la simulación. El alabeo rota alrededor del eje de vuelo.
- El descenso incorpora desplazamiento horizontal, una flecha de trayectoria y acercamiento del horizonte terrestre. Al desplegar paracaídas, la cabina se estabiliza y las velas permanecen sobre ella. La cámara general incluye las tres velas.
- La Tierra usa una esfera de mayor resolución, continentes estilizados, océanos, nubes, polos y una capa atmosférica. La Luna muestra mares y cráteres. Hay 4,800 estrellas con distintos tamaños, tonos y brillo.
- La bienvenida presenta comenzar/continuar, instrucciones y audio. ESC abre o cierra el menú; durante el menú la misión queda suspendida. P pausa y reanuda; F aborda; Espacio inicia el despegue; R reinicia.
- Seleccionar el campo de semilla ya no detiene la actualización del juego. La pausa también detiene la cuenta regresiva. El lanzamiento explica requisitos pendientes en lugar de quedar inaccesible. El HUD se ajusta dentro de un marco seguro de 1600 × 900 usando CanvasScaler Expand.

Se conservan las restricciones de fase: el ángulo de entrada se prepara en crucero y el alabeo se modifica durante la reentrada. Los límites de seguridad, presupuesto, clima y paracaídas siguen vigentes. La escala espacial se comprime para visualizar el vuelo; la telemetría conserva sus unidades físicas. Los continentes son una representación procedural estilizada.

## Verificación

- Compilación sin errores.
- 25/25 pruebas existentes de `MissionTests` aprobadas, incluyendo misión completa y límites físicos.
- `AgentScripts/VerifyReturnFlight.cs`: pruebas en Play Mode de pausa de cuenta regresiva, foco en semilla, suspensión y continuación desde menú, reinicio, conservación de las mallas planetarias y raycasts de los controles activos.
- Prueba del deslizador con −5.5° y −6.5°, orientación del escudo, desplazamiento horizontal/vertical y concordancia del ángulo visible con la trayectoria.
- Descenso con paracaídas, orientación de cabina y velas, amerizaje exitoso y contacto visual con el océano.
- Revisión visual de las capturas a 1280 × 720. Las demás proporciones usan el marco adaptable, pero no se ejecutó una matriz de resoluciones.
- Consola de la ejecución verificada: 0 errores y 0 advertencias. `git diff --check` sin problemas.

El script de verificación vive fuera de Assets, usa misiones deterministas y no se incluye en el jugador. Las pruebas no cambian los archivos de datos físicos.

## Capturas

![Bienvenida](Visuals/Aurora-Welcome.png)
![Reentrada](Visuals/Aurora-Reentry.png)
![Descenso](Visuals/Aurora-Landing.png)

Evaluación visual acotada a las superficies modificadas (1–5):

palette discipline: 4/5 — conserva azul oscuro, turquesa, naranja y cerámica clara.
style uniformity: 3/5 — interfaz y vehículos coherentes; geografía y paracaídas estilizados.
lighting depth: 3/5 — reflejo terrestre azul, materiales iluminados y borde atmosférico.
silhouette: 3/5 — cabina cerrada y escudo identificables; las velas caben completas.
UI intentionality: 4/5 — bienvenida propia, jerarquía tipográfica y botones consistentes.
feedback visibility: 4/5 — flecha de dirección, ángulo actual, orientación y despliegue progresivo.

La revisión corrigió el tamaño excesivo del planeta en la bienvenida, estrellas demasiado tenues, una cabina estrecha y velas cortadas por la cámara.

## Referencias y estado entregado

Guías consultadas: `unity-pipeline/SKILL.md`, `ui/SKILL.md`, `ui-ugui/SKILL.md`, `craft-game-floors/SKILL.md` y sus referencias `art-direction.md`, `game-shell.md`, `visual-scorecard.md`. Se mantuvo la paleta existente, sin aplicar cambios de audio o servicios ajenos a esta solicitud.

Documentación local del paquete Pipeline: `commands/scripts.md`, `commands/capture.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md`, `commands/wait.md`, `testing.md`. Documentación oficial: [Quaternion.FromToRotation](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/quaternion/fromtorotation) y [uGUI para múltiples resoluciones](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html).

Punto de restauración previo: commit `21a06ff` (árbol limpio al comenzar). No se instalaron paquetes ni se modificaron los FBX originales. Se añadieron scripts de presentación, un shader de atmósfera, el verificador y estas capturas. Unity queda en Play Mode con la bienvenida, misión reiniciada, auto-tick y ejecución en segundo plano activos. No se generó un ejecutable independiente.
