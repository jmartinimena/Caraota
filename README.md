<div align="center">
  
<img width="96" height="96" alt="caraota-logo" src="https://github.com/user-attachments/assets/d68c7a6e-3042-4559-b4f1-2f7ff7b2337a" />

# Caraota

![GitHub last commit](https://img.shields.io/github/last-commit/jmartinimena/Caraota?style=flat&color=brightgreen)
[![.NET 10](https://img.shields.io/badge/-%2010.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=flat&logo=csharp)
![Latency](https://img.shields.io/badge/Latency-80%C2%B5s-blueviolet?style=flat&logo=speedtest)
![Memory](https://img.shields.io/badge/Memory-Zero--Alloc-success?style=flat)
![Encryption](https://img.shields.io/badge/Crypto-Shanda%20%2F%20AES-informational?style=flat)
![License](https://img.shields.io/github/license/jmartinimena/Caraota?style=flat&color=yellow)

</div>

**Ultra-High-Performance Packet Interceptor & Logger for MapleStory v62**

Caraota es un motor de interceptación de red diseñado para la investigación de protocolos en servidores privados de MapleStory (específicamente v62). Caraota está optimizado para procesar paquetes en la escala de **nanosegundos**, garantizando una latencia casi nula y una estabilidad total del vector de inicialización (IV).

---

## 🚀 Benchmarks de Rendimiento

He llevado el rendimiento al límite técnico de C# y .NET 10, reduciendo el tiempo de procesamiento por paquete de **1,000,000 ns** a solo **~80,000 ns** (aun puede mejorar).

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

---

## 🔧 Requisitos e Instalación

1.  **.NET 10 SDK** o superior.
2.  **WinDivert**: Asegúrate de que `WinDivert.dll` y `WinDivert64.sys` estén presentes en el directorio de ejecución.
3.  **Privilegios de Administrador**: Necesarios para que el driver de WinDivert pueda abrir el handle del stack de red.

```bash
# Clonar el repositorio
git clone [https://github.com/jmartinimena/Caraota.git](https://github.com/jmartinimena/Caraota.git)

# Compilar en modo Release para máximo rendimiento
dotnet build -c Release
```

## ⚠️ Descargo de Responsabilidad (Disclaimer)

**POR FAVOR, LEA ESTO ATENTAMENTE ANTES DE UTILIZAR EL SOFTWARE.**

Este software, **Caraota**, se proporciona exclusivamente con fines **educativos, de investigación y de auditoría de seguridad de redes**. Al utilizar esta herramienta, usted acepta los siguientes términos:

1. **Uso Bajo su Propio Riesgo**: El autor de este software no se hace responsable de ningún daño, pérdida de datos, baneo de cuentas o consecuencias legales que resulten del uso de esta herramienta. El usuario asume toda la responsabilidad por las acciones realizadas con el software.
2. **Cumplimiento de Términos de Servicio**: El uso de herramientas de interceptación y manipulación de paquetes (MITM/Hijacking) puede violar los Términos de Servicio (ToS) de proveedores de juegos, servidores y servicios de red. El autor no fomenta ni respalda el uso de Caraota.NET para actividades que infrinjan acuerdos de licencia.
3. **Sin Garantías**: El software se distribuye "TAL CUAL" (AS IS), sin garantías de ningún tipo, expresas o implícitas, incluyendo, pero no limitado a, garantías de funcionamiento o idoneidad para un propósito específico.
4. **Finalidad Ética**: Esta herramienta fue diseñada para ayudar a desarrolladores y entusiastas de la ciberseguridad a comprender mejor el protocolo de red de MapleStory v62 y la arquitectura de red de alto rendimiento en .NET. No está destinada a ser utilizada para el beneficio desleal, robo de datos o interrupción de servicios de terceros.

**Si no está de acuerdo con estos términos, no haga uso del software.**
