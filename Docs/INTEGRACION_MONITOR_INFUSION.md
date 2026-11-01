# Monitor e infusión en ORI

Integración basada en ORI `f3d2f1b452e3337f731e2a815a72ac3aa28f903e`
y COPIA `64498e3fa387b88b09da46d0818150c5fe4057ff`.

El monitor principal utiliza los perfiles normal/reacción y la interfaz de COPIA
sobre el Canvas existente de ORI. Se conservan los destinos de los UnityEvents
del instructor y de ENVIAR. Temperatura, ECG y SpO2 reciben los valores de una
misma instancia de `MonitorTextos`; las referencias originales y sus CSV se
mantienen para instancias que no tengan asignado `perfilClinico`.

El conjunto sanguíneo conserva sus mallas de ORI. Una instancia del prefab NaCl
de COPIA conserva el enganche corregido y el módulo/palanca anteriores. El
controlador de infusión coordina únicamente los reguladores que comparten el
goteo sanguíneo y evita que los gestores heredados oculten o reabran las bolsas.
Se conserva el resto de la escena, incluidos Fusion, voz, paneles y medicamentos.

## Instalación local

Se necesita Paint in 3D instalado localmente. Sus archivos originales no forman
parte de esta transferencia. El prefab conserva los componentes de pintura de
la etiqueta y sus GUID de COPIA. Si Unity informa de un componente faltante en
esa etiqueta, comprobar la versión y los `.meta` del paquete instalado antes
de modificar el prefab. No eliminar sus componentes de pintura.

## Contrato con el panel

La validación de respuestas y el orden de las acciones pertenecen al panel.
En el snapshot de ORI integrado no hay un evaluador de las cinco acciones.
ENVIAR conserva las notificaciones y la recuperación de signos ya conectadas;
no abre NaCl ni valida por sí mismo una suspensión.

| Evento validado / interacción | Destino | Efecto |
| --- | --- | --- |
| Suspensión correctamente validada por el panel | `ControladorInfusionVisual.SuspenderSangreValidada()` | Autoriza el cierre manual. |
| Desplazamiento físico de la palanca de sangre hasta cerrar | Regulador táctil asignado | Detiene sangre y notifica `alCompletarAccionManual`. |
| NaCl correctamente validado, después del cierre sanguíneo | `ControladorInfusionVisual.ActivarNaClValidado()` | Autoriza la apertura manual. |
| Desplazamiento físico de la palanca NaCl hasta abrir | Regulador táctil del prefab | Activa NaCl y notifica `alCompletarAccionManual`. |
| Recuperación o desactivación de fiebre/taquicardia | Métodos existentes de ORI | Vuelve al perfil normal, sin cambiar el flujo. |
| Reinicio explícito del monitor | `MonitorTextos.ReiniciarMonitor()` | Vuelve a la primera muestra normal. |
| Reinicio explícito de la infusión | `ControladorInfusionVisual.ReiniciarPrueba()` | Restaura el estado inicial de sangre; es distinto de recuperar los signos. |

La reacción del monitor no suspende sangre automáticamente. La validación
tampoco confirma la ejecución del gesto. Los atajos de teclado y los
coordinadores de prueba de COPIA no se transfieren.

## Comprobación en Unity

1. Esperar la importación y comprobar consola y referencias de Paint.
2. Abrir `Escena_nueva`: comprobar el monitor y ambas bolsas antes de Play.
3. En Play, comprobar sangre fluyendo y NaCl visible sin gotas.
4. Pulsar INDUCIR FIEBRE: comprobar cambio de perfil, muestras y gráficas;
   repetir la orden no debe reiniciar la muestra. Comprobar paciente y oxímetro.
5. Comprobar recuperación, navegación de muñeca, tablet y sus cierres.
6. Cuando el panel tenga su evaluador conectado, probar autorización y gesto
   por separado: cierre de sangre y, después, apertura de NaCl.
7. Revisar posición de pantalla, enganche, sonda de 5 mm y colisiones en el
   Editor y en Quest. Probar también los dos roles si se utiliza Fusion.

Las comprobaciones automatizadas de esta transferencia son estáticas: sintaxis
C#, referencias serializadas, alcance de los documentos de escena y hashes.
La compilación del proyecto, Play y Meta Quest necesitan el Editor local.
No se ha añadido sincronización de estados clínicos a Fusion.
