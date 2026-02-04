# Caraota

**Ultra-High-Performance Packet Interceptor & Logger for MapleStory v62**

Caraota.NET es un motor de interceptación de red diseñado para la investigación de protocolos en servidores privados de MapleStory (específicamente v62). Caraota está optimizado para procesar paquetes en la escala de **nanosegundos**, garantizando una latencia casi nula y una estabilidad total del vector de inicialización (IV).

---

## 🚀 Benchmarks de Rendimiento

Hemos llevado el rendimiento al límite técnico de C# y .NET 8, reduciendo el tiempo de procesamiento por paquete de **1,000,000 ns** a solo **~80,000 ns**.

| Componente | Optimización | Impacto |
| :--- | :--- | :--- |
| **Criptografía** | Bitwise & Local Registers | ~2,500 ns |
| **Gestión de Memoria** | Zero-Allocation (ArrayPool) | 0 B Garbage |
| **Validación de IV** | Double-Buffer Logic | Estabilidad total |
| **Pipeline Global** | **Ultra-Low Latency** | **~80,000 ns** |

---

## 🛠️ Características Principales

### 1. Interceptación y MITM (Man-In-The-Middle)
Caraota.NET utiliza **WinDivert** para operar a nivel de Kernel, permitiendo no solo observar, sino interceptar y modificar el tráfico en tiempo real.
* **Packet Hijacking**: Modifica payloads (cambio de items, mensajes de chat, coordenadas) antes de que lleguen al destino.
* **Drop & Inject**: Descarta paquetes legítimos e inyecta secuencias personalizadas sin desincronizar la sesión TCP.
* **Auto-Checksum Correction**: Recalcula automáticamente los checksums de IP y TCP tras cualquier modificación del payload.



### 2. Ingeniería de "Zero-Allocation"
El motor está diseñado para evitar el Garbage Collector (GC) en el "Hot Path":
* **Uso de Spans & Memory**: Procesamiento de buffers mediante `ReadOnlySpan<byte>` para evitar copias costosas (`.ToArray()`).
* **Stackalloc**: Las semillas de actualización de IV se gestionan en el Stack, eliminando la presión sobre el Heap.
* **ArrayPool Integration**: Reutilización de buffers para el tráfico de red de alta intensidad.

### 3. Criptografía Avanzada v62
Implementación nativa y optimizada del protocolo de MapleStory:
* **Custom Shanda**: Rediseñado con rotación de bits (`ROL`) y carga en registros locales para máxima velocidad.
* **Fast Header Generation**: Generación de cabeceras mediante `BinaryPrimitives` y operaciones bitwise, eliminando divisiones y módulos lentos.
* **Double IV Sync**: Sistema inteligente que utiliza `LastIV` para recuperar la sincronía en caso de ráfagas de paquetes o micro-retrasos.

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
