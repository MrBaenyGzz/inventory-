# Cambios — Botones +/− de stock

**Rama:** `feature/botones-stock`
**Fecha:** 2026-06-25
**Autor:** Ignacio (con asistencia de Claude Code)

---

## Qué se agregó

Un **control rápido de stock** (stepper) en la barra superior, junto al botón **"+ Add Item"**.
Permite sumar o restar unidades al producto **seleccionado** en la lista, sin tener que abrir
la ventana de edición.

Se ve así:

```
[ − ]  [  1  ]  [ + ]     [ + Add Item ]
```

## Cómo funciona

1. Selecciona un producto en la lista.
2. Escribe en el campo del centro cuántas unidades quieres mover.
3. Pulsa **+** para sumar o **−** para restar. El cambio se aplica al instante.

### Reglas de comportamiento

- **Sin producto seleccionado**, los botones **+** y **−** están deshabilitados (grises).
- El campo de cantidad acepta **solo números enteros**, de **1 a 9999** (máx. 4 dígitos).
  Si el valor está fuera de rango, avisa y no aplica.
- **No se permite stock negativo**: si intentas restar más de lo que hay, muestra un aviso
  ("Not enough stock") y no realiza el cambio.
- Al aplicar, se **refresca la lista y el panel de detalle**, y se **guarda al instante** en
  `Data/inventory.txt`.

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `MainWindow.xaml` | Se agregó el stepper `[−] [campo] [+]` en el header. |
| `MainWindow.xaml.cs` | Lógica de sumar/restar, validación de rango, control de stock negativo, guardado y refresco. |
| `App.xaml` | Nuevo estilo `StepperButton` (botón compacto), coherente con el diseño existente. |

> Nota: la basura de compilación (`bin/`, `obj/`) **no** forma parte de estos cambios.

---

## Compatibilidad (importante para el equipo)

Estos cambios son **compatibles** con el resto del proyecto. **No se modificó**:

- El modelo `InventoryItem` (`Models/InventoryItem.cs`).
- El servicio `InventoryService` (`Services/InventoryService.cs`).
- El **formato del archivo** `Data/inventory.txt` (sigue siendo de 6 campos:
  `Id|Name|Quantity|Category|Description|ImageFile`).

Por lo tanto, no hay riesgo de chocar con otros cambios en curso sobre el modelo de datos
o la persistencia.

---

## Cómo probarlo

1. Cambiar a la rama: `git checkout feature/botones-stock`
2. Compilar y correr: `dotnet run -c Debug`  (o abrir en Visual Studio y pulsar F5)
3. Seleccionar un producto y probar los botones **+** / **−**.

---

## Estado

- ✅ Compila sin errores ni advertencias.
- ✅ Probado manualmente.
- ⏳ Pendiente de revisión del equipo antes de integrar a `main`.
