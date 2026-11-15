# Modos de juego y efectos de cápsula

Implementado y verificado el 15 de noviembre de 2026 en Unity 6000.6.9f1.

## Uso

Elige CASUAL o REALISTA en la bienvenida, o usa el selector situado sobre el panel de diseño. La elección se conserva al repetir y al generar otra semilla durante la sesión. Se bloquea al comenzar la cuenta regresiva. Una sesión nueva comienza en realista.

| Modo | Comportamiento |
| --- | --- |
| Casual | Conserva el crucero guiado, alabeo automático y paracaídas automáticos. Al confirmar la reentrada corrige el ángulo efectivo (incluidas las TCM) al intervalo −5.9°…−5.7°. Las probabilidades de anomalías de motores/SRB, agrietamiento, impactos y electricidad se multiplican por 0.15. |
| Realista | Conserva las probabilidades, límites y controles anteriores, incluidas las opciones manuales y el crucero libre. Incorpora la corrección del guiado automático descrita abajo. |

En ambos modos siguen vigentes presupuesto, viabilidad del diseño, clima, autonomía, consumo RCS, separaciones, calor, carga G y condiciones de amerizaje. Casual facilita el vuelo; no garantiza éxito ni elimina las anomalías. Las tablas JSON y las ecuaciones de `FlightPhysics.cs` no se modificaron.

## Mensaje de la captura

«Rebote atmosférico: ventana de descenso perdida» es una condición programada de derrota: pasan 7,200 segundos desde el comienzo de la reentrada sin alcanzar los 7,500 m que habilitan el descenso. No es una excepción de C#.

En la partida abierta se observó alabeo automático activo, ángulo preparado −5.707837° y una altitud final de unos 10,051 km. Ese estado final no registra todos los cambios manuales anteriores y por sí solo no permite reconstruir exactamente el vuelo.

Se encontró y corrigió un defecto independiente en el controlador automático: por encima de 9,000 m/s esperaba a que la trayectoria fuera ascendente **y** superara los 60 km antes de invertir la sustentación. Esa espera podía dejar salir la cápsula del aire donde todavía era posible controlarla. Ahora corrige en cuanto la trayectoria se vuelve ascendente, conservando el modelo físico y el guiado posterior por densidad.

La reproducción aislada nominal (9,350 kg, −6°, paso de 0.2 s) pasaba antes por un rebote de aproximadamente 948 km y tardaba 6,149 s en alcanzar el descenso. Con el ajuste llega en 568 s y mantiene el mismo máximo de 4.492 G. Las entradas demasiado pronunciadas y las maniobras manuales siguen pudiendo perder una misión en realista. El informe ahora explica el plazo de dos horas y propone reintentar cerca de −5.8° con alabeo automático.

## Presentación

- El antiguo óvalo oscilante del escudo se reemplazó por tres capas de partículas: borde caliente, envolvente turbulenta y estelas, con intensidad derivada del calor y la velocidad reales de la misión.
- Cuatro pequeñas toberas muestran pulsos de estabilización durante el ascenso después de separar LAS y durante el crucero. Las TCM y cambios de alabeo también activan pulsos visuales. Son ilustraciones de maniobra: no añaden empuje ni gasto de combustible al dominio.
- Las llamas tienen núcleo claro, tonos azules/ámbar, turbulencia, variación de tamaño y desvanecimiento. Los motores principales conservan su encendido y apagado por etapa.
- Pausa y menú detienen partículas y su reloj de shader. El plasma se extingue al pasar a paracaídas; los efectos se limpian al terminar y al reconstruir la nave.

La implementación visual está en `Assets/LaunchPrototype/CapsuleEffects.cs`; solo lee la simulación.

## Verificación

- Compilación final sin errores; consola actual con 0 errores y 0 advertencias.
- **34/34 pruebas de dominio aprobadas**, incluidas las 25 existentes y nueve casos nuevos: trayectoria nominal sin rebote prolongado, perfil de acero/Avcoat, corredor superficial, casual con −8° y −4°, conservación de controles realistas, riesgo reducido y bloqueo del modo en vuelo.
- `AgentScripts/VerifyGameModes.cs`: botones y raycasts, persistencia entre misiones, bloqueo durante cuenta regresiva, emisión de propulsores/plasma, pausa, menú, invariancia del estado físico, paracaídas, amerizaje y limpieza al reiniciar.
- Se ejecutaron también las seis regresiones de interacción de `AgentScripts/VerifyReturnFlight.cs`, incluidas pausa de cuenta regresiva, foco del campo de semilla y continuación desde menú.
- Se comprobó el avance real de frames mediante `job wait`, se leyeron los registros de verificación y se inspeccionaron capturas a 1280 × 720. No se hizo una matriz de dispositivos ni un ejecutable independiente.
- `git diff --check` sin errores de espacios.

![Selector de modos](Visuals/Aurora-Modes.png)
![Propulsores de la cápsula](Visuals/Aurora-Capsule-Jets.png)
![Plasma de reentrada](Visuals/Aurora-Plasma.png)
![Descenso con paracaídas](Visuals/Aurora-Modes-Landing.png)

## Estado y referencias de trabajo

Punto de restauración inicial: `95852573708acb4581c665960b15d98bf4afbd9e`; árbol limpio al empezar. Los cambios quedan locales, sin commit ni publicación. No se alteraron escenas, FBX, paquetes ni archivos de física JSON. El archivo de pruebas declara explícitamente las referencias de NUnit y Newtonsoft necesarias para inspeccionar los datos de configuración.

Unity queda en Play Mode, en la bienvenida, misión nueva con semilla 2026, modo realista y cámara general. Auto-tick y ejecución en segundo plano permanecen activos, como en la sesión de trabajo previa. Los verificadores viven fuera de Assets y no se incorporan al jugador.

Guías usadas: `unity-cli/SKILL.md`, `unity-pipeline/SKILL.md`, `ui/SKILL.md`, `ui-ugui/SKILL.md`. Referencias leídas: `unity-cli/references/playmode-verification-loop.md` y la documentación local de Pipeline `commands/scripts.md`, `commands/build-and-compilation.md`, `commands/editor-lifecycle-and-observability.md`, `commands/capture.md`, `commands/wait.md`.
