# Caraota

**Ultra-High-Performance Packet Interceptor & Logger for MapleStory v62**

Caraota.NET es un motor de interceptación de red diseñado para la investigación de protocolos en servidores privados de MapleStory (específicamente v62). A diferencia de otros loggers, Caraota está optimizado para procesar paquetes en la escala de **nanosegundos**, garantizando una latencia casi nula y una estabilidad total del vector de inicialización (IV).

---

## 🚀 Logros Tecnológicos (Benchmarks)

Gracias a una reingeniería profunda del flujo de datos, he logrado reducir la latencia de procesamiento de **~1,000,000 ns** (1ms) a tan solo **~80,000 ns** (0.08ms).

* **Zero-Allocation Pipeline**: Eliminación total de instanciaciones innecesarias en el Heap durante el ciclo de vida del paquete.
* **Nanosecond Precision**: Optimización de algoritmos criptográficos para ejecutarse en ciclos mínimos de CPU.
* **Kernel-Level Capture**: Uso de WinDivert para interceptación directa en el stack de red de Windows.

---

## 🛠️ Implementaciones Clave

### 1. Criptografía Optimizada (Zero-GC)
He rediseñado los algoritmos fundamentales de MapleStory para evitar el uso de memoria administrada:
* **Custom Shanda Shuffle**: Implementación que utiliza registros locales y operaciones de bits (`Bitwise Rotation`) en lugar de aritmética decimal pesada.
* **Fast Header Generation**: Generación de cabeceras de paquetes mediante `BinaryPrimitives` y máscaras de bits, eliminando divisiones y módulos costosos.
* **AES Integration**: Cifrado simétrico integrado directamente en el flujo de bytes mediante `Span<T>`.



### 2. Gestión de Memoria Inteligente
* **Buffer Pooling**: Uso de `ArrayPool<byte>.Shared` para manejar el tráfico de red sin disparar el Garbage Collector.
* **Stack Allocation**: Uso de `stackalloc` para datos temporales (como semillas de actualización de IV), manteniendo la memoria en la pila para una limpieza instantánea.
* **Ref Structs & Spans**: Todo el procesamiento se realiza mediante `ReadOnlySpan<byte>`, evitando copias de arrays (`.ToArray()`).

### 3. Sincronización de Sesión Avanzada
* **Double IV Validation**: Sistema de validación de doble vía que permite reintentar el descifrado utilizando el `LastIV` en caso de pérdida de sincronía por micro-retrasos de red.
* **Priority Scheduling**: Hilos de captura configurados con `ThreadPriority.Highest` y afinidad de núcleo para evitar interrupciones del Sistema Operativo.

---

## 📂 Estructura del Proyecto

El proyecto está dividido siguiendo principios de responsabilidad única y estandarización de .NET:

* **`Caraota.Crypto`**: Librería pura que contiene la implementación de AES, Shanda y la lógica de `MapleCrypto`. Independiente de la capa de red.
* **`Caraota.NET`**: El interceptor principal, eventos de sesión (`MaplePacketEventArgs`) y el wrapper de WinDivert.
* **`Native`**: Binarios nativos optimizados para arquitecturas `x64` y `x86`.

---

## 🔧 Requisitos e Instalación

1.  **.NET 8.0 SDK** o superior.
2.  **WinDivert**: Asegúrate de que `WinDivert.dll` y `WinDivert64.sys` estén presentes en el directorio de ejecución.
3.  **Privilegios de Administrador**: Necesarios para que el driver de WinDivert pueda abrir el handle del stack de red.

```bash
# Clonar el repositorio
git clone [https://github.com/jmartinimena/Caraota.NET.git](https://github.com/jmartinimena/Caraota.NET.git)

# Compilar en modo Release para máximo rendimiento
dotnet build -c Release
