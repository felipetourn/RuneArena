# RuneArena: cómo funciona el reconocimiento de runas

En RuneArena el jugador lanza hechizos dibujando runas en el aire con el control
de VR (Quest 2). El sistema de ML tiene que mirar ese trazo 3D y decidir cuál
de las 6 runas es, o si no es ninguna.

| Runa | Tipo de trazo |
|---|---|
| `square` (cuadrado) | cerrado |
| `circle` (círculo) | cerrado |
| `triangle` (triángulo) | cerrado |
| `lightning` (rayo, forma de Z) | abierto |
| `spiral` (espiral, del centro hacia afuera) | abierto |
| `v` | abierto |
| `noise` | **clase de rechazo**: garabatos, trazos a medias, formas que no son runas |

El proyecto pasó por tres etapas:

1. **$1 / Protractor**: comparar el trazo contra plantillas geométricas. Sin entrenamiento.
2. **CNN entrenada con trazos sintéticos**: un generador produce miles de trazos "dibujados a mano" con ruido controlado.
3. **CNN reentrenada con trazos reales del casco**: se mezclan los sintéticos con trazos grabados en el Quest 2.

Las tres comparten la misma captura y el mismo preprocesamiento. Esa parte va primero.

---

## Pipeline general

```
 Quest 2 (gatillo apretado)
        │
        ▼
 ┌──────────────────┐   puntos (x, y, z, t) en espacio del rig
 │  StrokeRecorder  │   + rotación de la cabeza al empezar
 └────────┬─────────┘
          ▼
 ┌──────────────────┐   1. proyectar al plano de la cabeza  → 2D
 │StrokePreprocessor│   2. re-muestrear a 64 puntos equidistantes
 │                  │   3. normalizar (centrar + escalar)
 └────────┬─────────┘
          ├─────────────────────────────┐
          ▼                             ▼
 ┌──────────────────┐         ┌──────────────────┐
 │    Protractor    │         │ rasterizar 28×28 │
 │ (64 puntos vs    │         └────────┬─────────┘
 │  plantillas)     │                  ▼
 └────────┬─────────┘         ┌──────────────────┐
          │                   │   CNN (ONNX)     │  Unity Inference Engine, CPU
          │                   └────────┬─────────┘
          ▼                            ▼
     score ≥ 0.85?              confianza ≥ 0.8 y ≠ noise?
          └──────────┬─────────────────┘
                     ▼   (un switch elige cuál decide)
              ┌──────────────┐
              │  RuneCaster  │  runa "cargada" → empujón o grip → hechizo
              └──────────────┘
```

---

## Paso 1: capturar el trazo

**Archivo:** [StrokeRecorder.cs](../Assets/Scripts/Strokes/StrokeRecorder.cs)

- Mientras el jugador mantiene el **gatillo derecho**, cada frame se toma la posición de la punta del control.
- Las posiciones se guardan en **espacio del rig** (relativas al XR Origin), así el trazo no depende de dónde esté parado el jugador en la arena.
- Un punto se agrega solo si está a **más de 6 mm** del anterior. Eso descarta el temblor cuando la mano está quieta.
- Máximo 512 puntos. Un trazo de menos de 8 puntos se descarta (fue un click, no un dibujo).
- Cada punto lleva también su **tiempo** desde el inicio del trazo: `(x, y, z, t)`.
- Al empezar se guarda la **rotación de la cabeza**. Es clave para el paso siguiente.

Con el modo de grabación activado (botón B izquierdo; el botón B derecho cambia la etiqueta), cada trazo se guarda como JSON en
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\RuneArena\strokes\<etiqueta>\`:

```json
{ "label": "circle", "source": "real", "device": "quest2",
  "headRotation": [x, y, z, w],
  "points": [[x, y, z, t], ...] }
```

Ese formato es el mismo para los trazos reales, los de mouse (editor) y los sintéticos. Por eso todo el pipeline de Python los trata igual.

---

## Paso 2: preprocesamiento (compartido por los dos reconocedores)

**Archivos:** [StrokePreprocessor.cs](../Assets/Scripts/Strokes/StrokePreprocessor.cs) y su espejo [ml/runes/preprocess.py](../ml/runes/preprocess.py)

### 2.1 Proyectar a 2D usando la cabeza
El trazo es 3D, pero el jugador lo dibuja "como en un pizarrón frente a su cara". Se rota cada punto por la **inversa de la rotación de la cabeza** al inicio del trazo y se descarta la profundidad. Queda `x` = derecha y `y` = arriba desde el punto de vista del jugador.

Por qué importa: si el jugador mira hacia otro lado, o dibuja inclinado, el cuadrado sigue siendo un cuadrado.

### 2.2 Re-muestrear a 64 puntos
La cantidad de puntos depende de la velocidad y del largo del trazo. Se recorre el trazo y se colocan **64 puntos equidistantes sobre la longitud del camino**. Ahora todos los trazos tienen el mismo tamaño y un trazo lento no pesa distinto que uno rápido.

### 2.3 Normalizar
- Se mueve el **centroide al origen** (no importa dónde se dibujó).
- Se escala uniformemente para que el **lado mayor del bounding box mida 1** (no importa el tamaño). Es uniforme para no deformar la figura.

El resultado son **64 puntos 2D normalizados**, que es la entrada de Protractor.

### 2.4 Rasterizar a 28×28 (solo para la CNN)
Los 64 puntos se dibujan como una imagen en escala de grises de **28×28 píxeles** con margen de 2 px. Entre cada par de puntos se interpolan 4 sub-pasos y cada sub-punto se reparte entre los 4 píxeles vecinos (**splatting bilineal**). Así la línea queda continua y suave, con valores entre 0 y 1.

28×28 es el mismo tamaño que MNIST: chico, rápido de procesar y suficiente para formas simples.

---

## Etapa 1: $1 Unistroke Recognizer → Protractor

**Archivo:** [ProtractorRecognizer.cs](../Assets/Scripts/Strokes/ProtractorRecognizer.cs), plantillas en [RuneShapes.cs](../Assets/Scripts/Strokes/RuneShapes.cs)

### Qué es el "$1"
El **$1 Unistroke Recognizer** (Wobbrock, Wilson y Li, 2007) es un algoritmo clásico de reconocimiento de gestos. Se llama "$1" porque es tan simple que "cuesta un dólar": unas 100 líneas de código, sin ML y sin entrenamiento. La idea:

1. Re-muestrear, rotar, escalar y centrar el trazo (lo mismo que el paso 2).
2. Compararlo punto a punto contra una **plantilla** de cada gesto.
3. Elegir la plantilla con menor distancia.

El problema del $1 original es que busca la mejor rotación **iterativamente** (golden section search), lo cual es lento.

### Protractor (Li, 2010), la versión que usamos
Protractor es la mejora del mismo grupo de autores. En lugar de una búsqueda iterativa:

- Trata los 64 puntos como **un vector de 128 números** y lo normaliza a longitud 1.
- La similitud es el **coseno** entre el vector del trazo y el de la plantilla.
- La **rotación óptima** entre ambos se calcula en **forma cerrada** con un `atan2` (una fórmula, sin iterar):

```
a = Σ (tx·cx + ty·cy)
b = Σ (tx·cy − ty·cx)
θ = clamp(atan2(b, a), −30°, +30°)
score = a·cos θ + b·sin θ        ∈ [−1, 1]
```

- La rotación se limita a **±30°**. Así un cuadrado inclinado sigue siendo cuadrado, pero un triángulo no se puede girar 180° y "convertirse" en un triángulo invertido.
- Si el mejor score es **≥ 0.85**, se acepta. Si no, se rechaza.

### Plantillas
No se graban: se generan desde las formas ideales en `RuneShapes`. Como Protractor compara punto a punto **en orden**, el mismo cuadrado empezado en otra esquina o dibujado en el otro sentido es un vector distinto. Por eso cada runa tiene **variantes**:
- Figuras cerradas: una por cada vértice de inicio × 2 sentidos (horario / antihorario).
- Figuras abiertas: hacia adelante y en reversa.
- Espiral: 8 ángulos de inicio × 2 sentidos de giro × 2 direcciones.

### Por qué no alcanzó
| A favor | En contra |
|---|---|
| Cero entrenamiento, cero datos | Compara contra formas **perfectas**; la mano en el aire no lo es |
| Rapidísimo (microsegundos) | Sensible a esquinas redondeadas, proporciones raras, cierres con hueco u overshoot |
| Fácil de explicar y depurar | **No tiene clase "no es nada"**: siempre elige la plantilla más parecida y solo un umbral decide el rechazo. Un garabato puede sacar 0.86 y disparar un hechizo |
| | Un umbral único sirve mal a todas las runas a la vez |

Protractor **sigue en el código**: corre en paralelo a la CNN, sirve como **baseline** de comparación y se puede activar con el switch `m_UseCnn` en [RuneRecognizer.cs](../Assets/Scripts/Strokes/RuneRecognizer.cs).

---

## Etapa 2: CNN entrenada con trazos sintéticos

La idea: en vez de comparar contra formas perfectas, **aprender** cómo se ve cada runa cuando la dibuja una mano real, incluyendo una clase explícita para lo que **no** es una runa.

El problema es que eso requiere miles de ejemplos etiquetados, y grabarlos a mano en el casco es lento. La solución fue **generarlos**.

### 2.1 Generador de trazos sintéticos
**Archivos:** [ml/runes/synth.py](../ml/runes/synth.py), [ml/synth.py](../ml/synth.py)

Se parte de la forma ideal de cada runa (las mismas variantes que usa Protractor) y se le aplican, en orden, las imperfecciones de una persona dibujando en el aire con un control:

| # | Transformación | Qué simula |
|---|---|---|
| 1 | **Mover vértices** (σ = 0.12) | Esquinas que no caen donde deberían: barras más cortas, punta del triángulo corrida |
| 2 | **Redondear esquinas** (75% de las veces, corner cutting de Chaikin) | Nadie hace esquinas a 90° perfectos en el aire |
| 3 | **Distorsión afín**: rotación ±15°, aspect ratio e^±0.7, leve shear; tamaño final 12–40 cm | Cuadrados "rectangulares", figuras torcidas, distintos tamaños |
| 4 | **Wobble**: 3 senoidales por eje, de 0.5 a 4 ciclos por trazo | Ondulación lenta del pulso |
| 5 | **Cierre imperfecto** (solo figuras cerradas): hueco al final o sigue de largo 3–15% | El final casi nunca cae exactamente en el inicio |
| 6 | **Recorte** de 0–3% al inicio y 0–5% al final | El gatillo se aprieta tarde o se suelta temprano |
| 7 | **Velocidad desigual**: duración 0.5–2 s muestreada a 72 fps, velocidad que oscila ±50% | El tiempo real de los frames del Quest |
| 8 | **Plano inclinado ±15° y curvado** + profundidad 30–55 cm | No se dibuja sobre un plano perfecto frente a la cara |
| 9 | **Temblor**: ruido gaussiano de 0.5–2 mm por punto | El jitter del tracking y de la mano |
| 10 | **Pose de cabeza aleatoria**: pitch ±30°, yaw 360°, roll ±5° | El jugador puede mirar hacia cualquier lado |
| 11 | **Mismo filtro que el recorder** (6 mm, máx. 512 puntos) | El trazo sintético pasa por el mismo filtro que uno real |

El resultado se guarda **en el mismo formato JSON que el StrokeRecorder**. Un trazo sintético pasa por exactamente el mismo preprocesamiento que uno real. El generador "falsifica" la entrada cruda, no la imagen final.

### 2.2 La clase `noise`
Para que la red aprenda a decir "esto no es una runa" hay que mostrarle no-runas:

| Proporción | Tipo |
|---|---|
| 30% | **Garabatos**: puntos al azar suavizados |
| 20% | **Polilíneas aleatorias** de 2 a 5 segmentos |
| 20% | **Runas a medias**: entre el 25% y el 65% de una runa real (el jugador soltó el gatillo antes) |
| 30% | **Hard negatives**: formas *parecidas* a runas que no lo son. Rombo (≈ cuadrado girado 45°), triángulo invertido, Z espejada (≈ rayo al revés) |

Los hard negatives obligan a la red a fijarse en lo que distingue una runa y no a aceptar "cualquier cosa con 4 esquinas".

Por defecto se generan **2000 trazos por clase × 7 clases = 14 000 trazos**, con semilla fija para que sea reproducible. El script también guarda una grilla de preview para ver los trazos a ojo.

### 2.3 Arquitectura de la red
**Archivo:** [ml/train.py](../ml/train.py) (`RuneNet`)

```
entrada 1×28×28 (la imagen rasterizada)
  │
  ├─ Conv 3×3, 16 filtros, padding 1 → ReLU → MaxPool 2×2   → 16×14×14
  ├─ Conv 3×3, 32 filtros, padding 1 → ReLU → MaxPool 2×2   → 32×7×7
  │
  ├─ Flatten                                                → 1568
  ├─ Linear 1568 → 64 → ReLU → Dropout 0.3
  └─ Linear 64 → 7                                          → logits
```

- **~106 000 parámetros** (≈ 420 KB en ONNX). Es una red muy chica, a propósito: tiene que correr en el CPU del Quest en una fracción de milisegundo.
- Las **convoluciones** detectan patrones locales (bordes, esquinas, curvas) en cualquier lugar de la imagen. El **pooling** da tolerancia a pequeños desplazamientos.
- El **dropout** apaga al azar el 30% de las neuronas durante el entrenamiento, para que la red no memorice los sintéticos.

Por qué una **imagen** y no la secuencia de puntos: la imagen no depende de dónde se empezó ni del sentido del trazo. Un cuadrado empezado en cualquier esquina, horario o antihorario, produce la misma imagen. Protractor necesitaba 8 plantillas para eso; la CNN lo resuelve sola.

### 2.4 Entrenamiento
- **Split**: 90% train / 10% validación, estratificado por clase.
- **Pérdida**: Cross-Entropy. **Optimizador**: Adam, lr = 1e-3. **Batch**: 128. **Épocas**: 12.
- Al final se imprime una **matriz de confusión** sobre validación. Con umbral, un `noise` predicho como `noise` o rechazado (`?`) cuenta como acierto.

### 2.5 Decisión: umbral + clase noise
La salida pasa por **softmax** para obtener probabilidades. La runa se acepta solo si:
1. la clase ganadora **no es `noise`**, y
2. su probabilidad es **≥ 0.8**.

Es un rechazo doble: la red puede decir explícitamente "esto es ruido", y además se rechaza cuando duda entre dos runas.

### 2.6 Exportar a Unity
- PyTorch exporta la red a **ONNX** (opset 15) en `Assets/Models/rune_cnn.onnx`.
- El script vuelve a correr el ONNX con **onnxruntime** y lo compara con PyTorch (diferencia máxima impresa, del orden de 1e-6).
- En Unity, [CnnRecognizer.cs](../Assets/Scripts/Strokes/CnnRecognizer.cs) carga el modelo con **Unity Inference Engine** (el sucesor de Sentis/Barracuda) en **backend CPU**: rasteriza, corre la red, aplica softmax y devuelve etiqueta + confianza.

### 2.7 Paridad Python ↔ C#
Hay dos implementaciones del preprocesamiento: Python para entrenar y C# para jugar. Si difieren aunque sea un poco, la red recibe en el juego imágenes distintas a las que vio en el entrenamiento, y falla sin que nada lo avise.

Para evitarlo:
- Cada archivo de Python dice en su primera línea de qué archivo C# es espejo.
- En Unity, el menú **RuneArena → Export Parity Data** ([ParityExport.cs](../Assets/Scripts/Strokes/Editor/ParityExport.cs)) procesa los trazos de `ml/data/mouse/` con el código C#.
- [ml/parity.py](../ml/parity.py) procesa los mismos trazos en Python y compara píxel a píxel la imagen y el resultado de Protractor (tolerancia 1e-3).

---

## Etapa 3: reentrenar con trazos reales del casco

Los sintéticos son una aproximación. El casco tiene particularidades que el generador no captura del todo: cómo trackea el Quest 2, cómo cada persona sostiene el control, las mañas propias al dibujar cada runa. La etapa 3 agrega **datos reales**.

### 3.1 Grabar
1. Poner el casco y entrar a la escena con el StrokeRecorder.
2. Activar la grabación (**B izquierdo**; aparece "REC" en rojo arriba a la derecha).
3. Elegir la etiqueta con **B derecho**. El panel muestra cuántos trazos hay de cada una.
4. Dibujar varias veces cada runa, y también **trazos de `noise`** a propósito: garabatos, runas a medias, formas parecidas.
5. Los JSON quedan con `"device": "quest2"` en la carpeta `strokes/<etiqueta>/` del dispositivo, que después se copia a la PC.

### 3.2 Entrenar con la mezcla
```bash
cd ml
python train.py --real <carpeta_con_trazos_del_casco>
```

Detalles importantes:

- **No es fine-tuning** del modelo anterior: se **reentrena desde cero** con *sintéticos + reales* juntos. Es más simple y evita que la red "olvide" la variedad de los sintéticos.
- **Split de los reales: 60% train / 40% test** (`--real-test 0.4`). El 40% de test **nunca se usa para entrenar**. La lista exacta se guarda en `data/real_test_split.txt` para poder reusarla.
- **Peso ×10** (`--real-weight 10`): cada trazo real de entrenamiento se repite 10 veces. Hay pocos reales frente a 14 000 sintéticos, y sin ese peso la red casi no los notaría.
- El script imprime **dos matrices de confusión**: validación sintética y **test real**. La que importa es la segunda, porque son trazos reales del casco que la red nunca vio.
- Exporta el nuevo `rune_cnn.onnx` y Unity lo toma automáticamente.

### 3.3 Comparar contra el baseline
```bash
python evaluate.py --split data/real_test_split.txt
```
Corre **Protractor y la CNN sobre exactamente los mismos trazos reales de test** e imprime la matriz de confusión de cada uno. Es la comparación justa para mostrar en la presentación: mismos datos, dos métodos.

---

## En el juego

[RuneRecognizer.cs](../Assets/Scripts/Strokes/RuneRecognizer.cs):
- Cuando termina un trazo, preprocesa una sola vez y corre **los dos** reconocedores.
- Mide el tiempo de cada uno con `Stopwatch`. En modo debug o grabación muestra `P: …` y `C: …` con etiqueta, score y milisegundos.
- El switch `m_UseCnn` decide cuál manda. Por defecto, la CNN.

[RuneCaster.cs](../Assets/Scripts/Strokes/RuneCaster.cs):
- **Rechazado** → el trazo se pinta de rojo y se desvanece.
- **Reconocido pero sin maná o bloqueado** → gris.
- **Reconocido** → el trazo toma el color de la runa y queda "cargado" hasta 4 s.
- Se lanza con un **empujón** del control hacia adelante (> 1.5 m/s durante 3 frames) o con el **grip / A**.

---

## Resultados

> Completar con la salida real de `train.py --real` y `evaluate.py --split`.
> No inventar números: la audiencia puede preguntar de dónde salen.

| Método | Datos de entrenamiento | Accuracy en test real (casco) | Tiempo por trazo |
|---|---|---|---|
| Protractor | ninguno (plantillas) | __ % | __ ms |
| CNN | solo sintéticos | __ % | __ ms |
| CNN | sintéticos + reales del casco | __ % | __ ms |

Conviene mostrar también la **matriz de confusión** de la CNN final. Deja ver qué runas se confunden entre sí (candidatos típicos: `v` ↔ `lightning`, `circle` ↔ `spiral`, `square` ↔ rombo/noise).

---

## Comandos (resumen)

```bash
cd ml
pip install -r requirements.txt                       # numpy, torch, onnx, onnxruntime

python synth.py                                       # 1. genera data/synthetic/ (2000 por clase) + preview
python train.py                                       # 2. CNN solo con sintéticos → Assets/Models/rune_cnn.onnx
python train.py --real <trazos_del_casco>             # 3. CNN con sintéticos + reales
python evaluate.py --split data/real_test_split.txt   # 4. Protractor vs CNN en el test real
python parity.py                                      # 5. (tras Export Parity Data en Unity) verifica C# == Python
```

---

## Preguntas probables de la audiencia

**¿Por qué no entrenar solo con datos reales?**
Hacen falta miles de ejemplos y grabarlos es lento. Los sintéticos dan volumen y variedad (poses, tamaños, velocidades); los reales corrigen lo que el generador no imita bien. La mezcla aprovecha las dos cosas.

**¿Por qué una CNN sobre imagen y no una RNN/LSTM sobre la secuencia de puntos?**
La imagen ignora por dónde se empezó y en qué sentido se dibujó, que para estas runas no importa. Una red sobre secuencias tendría que aprender esa invariancia con más datos. Además, una CNN de 28×28 es diminuta y muy rápida.

**¿Cómo evitan que un garabato dispare un hechizo?**
Con dos barreras: la clase `noise`, entrenada con garabatos, runas a medias y formas parecidas, y el umbral de confianza de 0.8.

**¿Corre en el casco?**
Sí. Corre en el CPU del Quest con Unity Inference Engine. La red tiene ~106 k parámetros y el tiempo se mide en cada trazo (ver la tabla de resultados).

**¿Qué pasa si el jugador dibuja mirando para otro lado o inclinado?**
El trazo se proyecta en el plano de la cabeza al inicio del dibujo. El generador sintético además varía la pose de la cabeza en 360° y la inclinación del plano, así que la red ya vio esos casos.

**¿Por qué siguen teniendo Protractor?**
Como baseline para medir la mejora de la CNN, y como respaldo que no necesita modelo.

**¿Cómo saben que Unity procesa los trazos igual que Python?**
Con la verificación de paridad: el mismo set de trazos pasa por los dos pipelines y se compara píxel a píxel.

---

## Anexo: CNN, ONNX y Unity Inference Engine en detalle

Este anexo sigue el camino real de una runa: qué hace la red, en qué archivo queda guardada y quién la ejecuta en el juego.

### A.1 CNN (Convolutional Neural Network)

#### Una red neuronal, primero
Una red neuronal es una función matemática grande con muchos números ajustables adentro, los **pesos**. Entra un dato (una imagen) y sale una respuesta (7 números, uno por runa). Al principio los pesos son aleatorios y la respuesta es basura.

**Entrenar** es mostrarle miles de ejemplos con la respuesta correcta. Cada vez que se equivoca, un algoritmo (Adam) corrige un poquito cada peso en la dirección que reduce el error. Después de miles de correcciones, los pesos quedan en valores que hacen que la función "reconozca runas". Nadie escribió reglas: las reglas quedaron codificadas en esos ~106 000 números.

#### Qué tiene de especial la "C"
Una red común trataría la imagen de 28×28 como una lista de 784 números sueltos, sin saber qué píxel está al lado de cuál. Ahí se pierde lo más importante de una imagen: la **vecindad**.

La CNN usa **filtros**: grillitas de 3×3 pesos que se deslizan por toda la imagen. En cada posición multiplican los 9 píxeles de abajo por sus 9 pesos y suman. Si esa zona tiene el patrón que el filtro "busca" (por ejemplo, una línea diagonal), da un número alto. Si no, da uno bajo. El resultado es un **mapa** de dónde aparece ese patrón en la imagen.

Dos ideas clave:
- **El mismo filtro se usa en toda la imagen.** Una esquina arriba a la izquierda y una abajo a la derecha las detecta el mismo filtro. Por eso necesita pocos pesos y no le importa dónde está cada cosa.
- **Los filtros no se programan, se aprenden.** El entrenamiento decide solo qué patrones conviene buscar.

#### La red de RuneArena, capa por capa
```
imagen 28×28 (el trazo rasterizado)
 → 16 filtros 3×3   → 16 mapas de 28×28   "¿dónde hay bordes/líneas de cada tipo?"
 → ReLU             → negativos a 0       (sin esto todo sería una sola multiplicación lineal)
 → MaxPool 2×2      → 16 mapas de 14×14   (achica y tolera corrimientos chicos)
 → 32 filtros 3×3   → 32 mapas de 14×14   combinan lo anterior: "esquina", "arco", "cruce"
 → ReLU + MaxPool   → 32 mapas de 7×7
 → aplanar          → 1568 números
 → Linear → 64      "resumen" de qué rasgos aparecieron y dónde
 → Linear → 7       un puntaje por clase (logits)
 → softmax          → probabilidades que suman 1
```
La primera capa ve detalles chicos (3×3 píxeles). La segunda, al trabajar sobre una imagen ya achicada, "ve" zonas más grandes y combina detalles en partes de figuras. Las capas lineales del final hacen el razonamiento: "4 esquinas rectas + lados rectos + cerrado = square".

Casi todos los parámetros (~100 k de los ~106 k) están en la capa `1568 → 64`. Las convoluciones son baratísimas.

#### Por qué una CNN en este caso
Al convertir el trazo en imagen se pierde el orden de los puntos: no importa dónde se empezó ni en qué sentido se dibujó. Protractor necesitaba 8 plantillas por cuadrado; la CNN ve siempre el mismo cuadrado. Además es un problema muy parecido a MNIST (dígitos escritos a mano en 28×28), que es justo lo que las CNN chicas resuelven muy bien.

### A.2 ONNX (Open Neural Network Exchange)

Cuando termina el entrenamiento, el modelo existe **dentro de PyTorch, en Python**. Pero el juego es C# en Unity, y Unity no puede ejecutar código PyTorch.

Una red neuronal entrenada son en realidad dos cosas:
1. **El grafo:** la receta de operaciones en orden ("convolución con estos tamaños → ReLU → maxpool → … → linear").
2. **Los pesos:** los ~106 000 números aprendidos.

**ONNX es un formato de archivo estándar que guarda exactamente esas dos cosas**, de forma que no depende de quién lo creó. Las operaciones tienen nombres y definiciones fijas: `Conv`, `Relu`, `MaxPool`, `Gemm`. Cualquier programa que implemente esas operaciones puede ejecutar el modelo.

En `train.py` pasa esto:
- `torch.onnx.export(...)` corre la red una vez con una imagen de prueba, **registra qué operaciones se ejecutaron** y las escribe junto con los pesos en `rune_cnn.onnx`.
- `opset_version=15` es la **versión del "vocabulario"** de operaciones ONNX. Hay que elegir una que el lector (Unity) soporte.
- Después abre ese archivo con **onnxruntime** (otro lector de ONNX, en Python) y compara sus salidas con las de PyTorch. Si la diferencia es ~1e-6, la exportación está bien.

Lo importante: **ONNX no entrena ni ejecuta nada.** Es solo el formato del archivo, como un `.pdf` o un `.fbx` para modelos 3D. Lo que hace es desacoplar las herramientas: se entrena donde es cómodo (Python) y se ejecuta donde hace falta (Unity en el Quest).

### A.3 Unity Inference Engine

Es el **paquete de Unity que lee un archivo ONNX y lo ejecuta en runtime**, dentro del juego y en cualquier plataforma que soporte Unity, incluido el Quest. Es la tercera generación de lo mismo: **Barracuda → Sentis → Inference Engine** (el namespace actual es `Unity.InferenceEngine`).

Cómo lo usa [CnnRecognizer.cs](../Assets/Scripts/Strokes/CnnRecognizer.cs):

1. **Importar:** al arrastrar el `.onnx` a `Assets/`, Unity lo convierte en un `ModelAsset`. En ese momento revisa que todas las operaciones del grafo estén soportadas.
2. **Cargar:** `ModelLoader.Load(modelAsset)` lo pasa a una representación en memoria.
3. **Crear un Worker:** `new Worker(model, BackendType.CPU)`. El *worker* es el motor que ejecuta el grafo. El **backend** elige dónde corren las cuentas:
   - `CPU`: en el procesador. Es el que se usa, porque la red es chica y la GPU del Quest está ocupada renderizando a 72 fps.
   - `GPUCompute`: en la placa de video, con compute shaders. Conviene para redes grandes.
4. **Cada trazo:**
   - Se arma un `Tensor<float>` con forma `(1, 1, 28, 28)`. Un **tensor** es un array multidimensional; acá es batch de 1 imagen, 1 canal (escala de grises), 28×28.
   - `Schedule(input)` ejecuta el grafo.
   - `PeekOutput().DownloadToArray()` devuelve los 7 logits como un `float[]`.
   - El softmax y el umbral se aplican en C#.
5. **`Dispose()`** al destruir el objeto, para liberar la memoria nativa del worker.

Todo pasa **en el casco, local**: sin servidor, sin internet. Para una red de este tamaño tarda una fracción de milisegundo; se ve en el texto de debug `C: … (x ms)`.

### A.4 Cómo encajan las tres

```
PyTorch (Python, en la PC)       →  rune_cnn.onnx        →  Inference Engine (C#, en el Quest)
define y ENTRENA la CNN             guarda grafo + pesos      CARGA y EJECUTA la CNN en el juego
```

- **CNN** = *qué* es el modelo (la arquitectura y lo que aprendió).
- **ONNX** = *cómo se guarda* para viajar entre herramientas.
- **Inference Engine** = *quién lo ejecuta* dentro de Unity.

**Lo único que puede romperse entre los dos mundos es el preprocesamiento.** El ONNX garantiza que la red es idéntica en Python y en Unity, pero la imagen 28×28 que recibe la arma código propio, escrito dos veces (`preprocess.py` y `StrokePreprocessor.cs`). Por eso existe el chequeo de paridad (sección 2.7): si esas dos imágenes difieren, la red recibe algo que nunca vio y falla sin avisar.

---

## Referencias
- Wobbrock, J. O., Wilson, A. D., & Li, Y. (2007). *Gestures without libraries, toolkits or training: A $1 recognizer for user interface prototypes.* UIST '07.
- Li, Y. (2010). *Protractor: A fast and accurate gesture recognizer.* CHI '10.
- Chaikin, G. M. (1974). *An algorithm for high-speed curve generation.* (el redondeo de esquinas del generador)
