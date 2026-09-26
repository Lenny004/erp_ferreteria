# Vista previa de tickets

Genera texto plano y bytes ESC/POS ficticios, sin WPF, base de datos ni impresora:

```text
dotnet run --project tools/Ferreteria.TicketPreview -- <carpeta-salida>
```

Produce `ticket-58mm.txt/.bin` y `ticket-80mm.txt/.bin`. Los datos contienen la marca `PRUEBA` y no son fiscales.
