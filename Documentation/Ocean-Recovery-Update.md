# AURORA — descenso y amerizaje animados

15 de noviembre de 2026 · Unity 6000.6.9f1 · URP 17.6 (`PC_RPAsset`).

El lugar del amerizaje ahora tiene un océano con textura procedural continua, tres ondas superpuestas, detalle superficial desplazándose, reflejos, espuma de contacto y ondas expansivas. Un cielo con textura de nubes en movimiento, luz solar y bruma cubre el fondo. Se conserva la paleta azul/turquesa, naranja de recuperación y cerámica clara del proyecto.

La cápsula oscila y se inclina durante el descenso. Los paracaídas tienen cúpulas abiertas con ventilación central, franjas naranjas y claras, cuerdas y apertura progresiva. La reproducción normal del descenso pasa de ×12 a un máximo de ×6 y disminuye progresivamente hasta ×1.25 cerca del agua; los botones de velocidad siguen multiplicando esta tasa. La telemetría y las condiciones físicas de éxito permanecen a cargo de `Mision`.

Al tocar el agua se dispara una salpicadura reutilizable, las velas se desinflan y se retiran, y la cámara general se acerca durante siete segundos. La cápsula sigue las mismas ondas que dibuja el agua y mantiene el escudo parcialmente sumergido después de aparecer el informe. La cámara de cabina permite inspeccionar el contacto más de cerca. Pausa y menú suspenden también esta animación; reiniciar limpia la espuma, las partículas y el estado de recuperación. La plataforma de lanzamiento se oculta durante el regreso y el amerizaje.

## Verificación

- Compilación C# y shaders correctos en el dispositivo gráfico activo.
- 34/34 pruebas existentes de `MissionTests` aprobadas.
- `AgentScripts/VerifyRecovery.cs`, ejecutado en Play Mode: vuelo determinista hasta el regreso, despliegue e inflación, reducción de velocidad, superficie y cielo visibles, estructuras de lanzamiento ocultas, pausa de trayectoria y oleaje, aproximación, salpicadura con 140 partículas, flotación posterior al resultado, conservación de telemetría, suspensión desde menú y reinicio.
- Velocidad física de contacto del caso verificado: 8.03977 m/s; separación del centro de la cápsula respecto al nivel base del mar durante la flotación: aproximadamente 0.24 unidades visuales.
- Diez salpicaduras consecutivas reutilizan el mismo sistema de partículas. El reinicio conserva la malla del océano y no duplica emisores.
- Consola: cero errores y cero advertencias. `git diff --check` correcto.
- Capturas revisadas a 1280 × 720, con HUD. No se generó un ejecutable independiente ni se midió rendimiento en otras plataformas.

## Revisión visual acotada

La primera pasada reveló normales invertidas en las velas, estructuras de la plataforma visibles en el mar y ondulación distante demasiado repetitiva. Se corrigieron la orientación de las caras, la visibilidad del entorno terrestre, la atenuación del detalle lejano y el grosor de la espuma junto a la cápsula.

palette discipline: 4/5 — océano turquesa, velas naranja/crema y cápsula cerámica; antes 3/5.

style uniformity: 3/5 — entorno y vehículo conservan el acabado estilizado existente; antes 3/5.

lighting depth: 4/5 — reflejo solar fragmentado, horizonte con bruma y tela correctamente iluminada; antes 2/5.

silhouette: 4/5 — velas legibles durante el descenso y encuadre cercano tras el contacto; antes 3/5.

UI intentionality: 4/5 — HUD conservado, estado de pausa coherente después del resultado; antes 4/5.

feedback visibility: 4/5 — despliegue, balanceo, salpicadura, ondas y flotación persistente; antes 2/5.

![Descenso](Visuals/Aurora-Ocean-Descent.png)
![Aproximación](Visuals/Aurora-Ocean-Approach.png)
![Contacto](Visuals/Aurora-Ocean-Splash.png)
![Flotación](Visuals/Aurora-Ocean-Floating.png)
![Vista cercana](Visuals/Aurora-Ocean-Closeup.png)

## Archivos y estado

Se añadieron `RecoveryEnvironment.cs`, `AuroraOcean.shader`, `AuroraRecoverySky.shader`, sus metadatos, el verificador y estas capturas. Se integraron en `LaunchDemo.cs`, `MissionPresentation.cs`, `ReturnFlightPresentation.cs` y el indicador de pausa de `LaunchHud.cs`. No se instalaron paquetes. Las texturas y las mallas del entorno se construyen en ejecución y se liberan con los recursos generados del controlador. No hay componentes de verificación dentro del jugador.

Había cambios del usuario sin confirmar al comenzar; se preservaron. Copias de los cuatro archivos antes de esta edición: `Temp/SplashdownRestore-20261115/`. El commit previo del repositorio es `9585257`, pero esas copias también incluyen los cambios locales anteriores a este trabajo. No se creó ningún commit.

Unity comenzó con `SimulatorGame` en Play Mode, pausado después de un amerizaje exitoso. La revisión utilizó una misión determinista. Se deja nuevamente en Play Mode pausado, en una vista cercana del amerizaje; se puede quitar la pausa del Editor para observar el oleaje. La ejecución en segundo plano y auto-tick permanecen activos.

## Guías consultadas

`unity-pipeline/SKILL.md`; documentación instalada de Pipeline: `commands/scripts.md`, `commands/capture.md`, `commands/editor-lifecycle-and-observability.md`, `commands/build-and-compilation.md`, `commands/runtime.md` y `commands/wait.md`.

`craft-game-floors/SKILL.md` y referencias `art-direction.md`, `game-feel.md`, `vfx.md`, `visual-scorecard.md`. Aplicación acotada al entorno, movimiento y efectos de recuperación; se conserva la dirección visual existente. No se aplicaron cambios de audio, servicios ni interfaz ajenos a esta solicitud.

Documentación oficial consultada: [Time.deltaTime](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/deltatime), [ObjectPool](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/pool/objectpool1) y [Application.runInBackground](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/application/runinbackground).
