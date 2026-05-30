param(
  [string]$Out = "capture.png",
  [int]$TopCrop = 110,        # Recorta tabs de Chrome + URL bar
  [int]$BottomCrop = 50       # Recorta barra de tareas de Windows
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$w = $screen.Width
$h = $screen.Height

# Full screenshot
$full = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($screen.X, $screen.Y, 0, 0, $full.Size)

# Crop top + bottom
$cropH = $h - $TopCrop - $BottomCrop
$cropped = New-Object System.Drawing.Bitmap $w, $cropH
$gc = [System.Drawing.Graphics]::FromImage($cropped)
$srcRect = New-Object System.Drawing.Rectangle 0, $TopCrop, $w, $cropH
$dstRect = New-Object System.Drawing.Rectangle 0, 0, $w, $cropH
$gc.DrawImage($full, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)

# Save PNG
$cropped.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose(); $gc.Dispose(); $full.Dispose(); $cropped.Dispose()
Write-Output "Saved: $Out ($($w)x$($cropH))"
