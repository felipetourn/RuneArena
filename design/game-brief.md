# Game Brief: RuneArena

**One-sentence pitch:** Dibujá runas en el aire con tus manos para lanzar hechizos y sobreviví a las oleadas y los jefes de un coliseo en VR.

## Core loop
- Dibujás una runa en el aire → el juego la reconoce y lanza el hechizo (gasta maná).
- Matás la oleada de enemigos → soltan vida y maná.
- Entre oleadas elegís una mejora → la siguiente oleada es más dura, y cada etapa termina en un jefe.

## Player goal & fail state — what "working" looks like
- Llegar lo más lejos posible: cada etapa completada (oleadas + jefe) cuenta.
- La partida termina cuando la vida llega a 0 (Game Over); reiniciar recarga la escena y empieza de cero.

## MVP — what must exist to be the game
- Reconocimiento de 6 runas dibujadas con manos/controllers (cuadrado, círculo, rayo, triángulo, espiral, V).
- 6 hechizos, uno por runa: Pared, Bola de fuego, Rayo, Ola, Hiedra, Hielo, con maná.
- Enemigos de varios tipos (cuerpo a cuerpo, a distancia, voladores, sanadores, con armadura).
- Oleadas agrupadas en etapas que se repiten con más enemigos.
- Un jefe al final de cada etapa, cada uno con identidad propia (Tillagemon, Gideon, Megademon, dragón): barra de furia que alterna golpes simples con ataques especiales, y zonas de peligro que avisan los ataques.
- Mejoras entre oleadas, por rareza (Bronce a Diamante).
- Vida, maná, pickups, menú de inicio y Game Over.

## Out of scope — not building this
- Multijugador.
- Progreso guardado entre partidas (la tabla de puntajes es lo único que se guardará, pospuesta: ver orden de construcción).
- Solo Quest (Android); sin PC VR.
- Tutorial o práctica guiada de las runas (riesgo abierto: un jugador nuevo no sabe las 6 runas).
- Opciones de comodidad VR (de pie/sentado, zurdos): por definir.

## Build order
1. Reconocimiento de runas (el riesgo mayor; ya construido).
2. Hechizos, maná y enemigos con oleadas (ya construido).
3. Etapas, jefes y mejoras (construido).
4. **En el casco — rendimiento:** build al Quest y medir FPS (OVR Metrics Tool u overlay) contra el jefe más pesado y con muchos enemigos. Si baja de 72, anotar en qué momento, y con eso decidir qué recortar.
5. **En el casco — trazos reales:** grabar 30–50 por runa con el StrokeRecorder, incluida la clase "noise" y ojalá de otras personas. Bajarlos con `adb pull`, reentrenar con `python ml/train.py --real <carpeta>` y comparar contra Protractor con `python ml/evaluate.py`.
6. **En el casco — sensación:** ajustar lo que el simulador no muestra: escala de los jefes (Gideon mide 3,2 m), hitboxes y velocidad de los jefes.
7. Tabla de puntajes guardada y visible en el juego (pospuesta, no es parte del MVP).

## Pendientes a futuro
- **Opción en el menú para mostrar u ocultar la línea de los controles.** Hoy se ve siempre entera:
  `m_LineDynamicsMode` en Traditional en `Left_NearFarInteractor.prefab` (el derecho es una variante y la
  hereda). La opción cambiaría `CurveVisualController.lineDynamicsMode` (o apagaría la línea) en los dos
  controles. Va junto con las opciones de comodidad VR, todavía por definir.
- **Reducir el esqueleto mago (enemigo a distancia) en Blender:** ~100k triángulos, apuntar a ~12k como el
  esqueleto melee. Guardarlo como `SkeletonMage.fbx` y correr RuneArena > Create Ranged Mage Model.

---
**Who it's for / what they feel:** Jugadores de VR en Quest que quieren sentirse magos de verdad lanzando hechizos con el cuerpo.

**Art & audio direction:** Fantasía estilizada de coliseo con runas, shaders de hechizo estilizados y efectos de fuego, humo y luz. UI con la fuente Metamorphous y marcos rúnicos en los menús. Audio: música por escena y efectos para hechizos, enemigos y jefes.

**Reference game:** Vampire Survivors — oleadas y mejora entre rondas, sin progreso fuera de la partida; llevado a VR con gestos.
