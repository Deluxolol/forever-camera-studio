$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field([string]$name){,$type.GetField($name,$flags).GetValue($window)}
function Order {($points | ForEach-Object {$_.X}) -join ','}
function ReorderPoint($point,[int]$slot){$type.GetMethod('MoveWaypoint',$flags).Invoke($window,[object[]]@($point,$slot)) | Out-Null}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $points=Field 'points';$grid=Field 'grid';$motion=Field 'motion'
    $original=@()
    foreach($i in 1..4){$p=[Machinae.Forever.CameraPoint]::new();$p.X=$i;$p.Y=$i*10;$p.Z=$i*100;$p.Yaw=$i*5;$p.Fov=80+$i;$original+=,$p;$points.Add($p)}
    ReorderPoint $original[0] 4
    if((Order) -ne '2,3,4,1' -or -not [object]::ReferenceEquals($grid.CurrentRow.DataBoundItem,$original[0])){throw 'First-to-last move or selection failed.'}
    ReorderPoint $original[2] 0
    if((Order) -ne '3,2,4,1'){throw 'Middle-to-first move failed.'}
    ReorderPoint $original[2] 1
    if((Order) -ne '3,2,4,1'){throw 'Dropping beside the same row changed the order.'}
    # Exercise the real drag-over/drop handlers and their insertion target.
    $type.GetField('draggedWaypoint',$flags).SetValue($window,$original[0])
    $type.GetField('waypointDragToken',$flags).SetValue($window,'order-test')
    $data=[Windows.Forms.DataObject]::new();$data.SetData('ForeverCameraStudio.WaypointOrder',$false,'order-test')
    $row=$grid.GetRowDisplayRectangle(0,$false);$screen=$grid.PointToScreen([Drawing.Point]::new(50,$row.Top+2))
    $drag=[Windows.Forms.DragEventArgs]::new($data,1,$screen.X,$screen.Y,[Windows.Forms.DragDropEffects]::Move,[Windows.Forms.DragDropEffects]::None)
    [Windows.Forms.DataGridView].GetMethod('OnDragOver',$flags).Invoke($grid,@($drag)) | Out-Null
    if($drag.Effect -ne [Windows.Forms.DragDropEffects]::Move -or (Field 'waypointDropSlot') -ne 0){throw 'Drag-over did not accept the target row.'}
    [Windows.Forms.DataGridView].GetMethod('OnDragDrop',$flags).Invoke($grid,@($drag)) | Out-Null
    if((Order) -ne '1,3,2,4'){throw 'Drag-drop did not reorder the bound points.'}
    foreach($point in $original){if(-not $points.Contains($point) -or $point.Y -ne $point.X*10 -or $point.Z -ne $point.X*100 -or $point.Fov -ne 80+$point.X){throw 'A waypoint was lost or its pose changed.'}}
    $project=$type.GetMethod('Project',$flags).Invoke($window,@())
    $file=Join-Path $PSScriptRoot 'test-results/waypoint-order.xml';$project.Save($file)
    $loaded=[Machinae.Forever.CameraProject]::Load($file)
    if((($loaded.Points | ForEach-Object {$_.X}) -join ',') -ne '1,3,2,4'){throw 'Saved/loaded project lost the new order.'}
    $motion.Start($points,20,$false,$false)
    $blocked=$false;try{ReorderPoint $points[0] 4}catch{$blocked=$_.Exception.InnerException -is [InvalidOperationException]}
    if(-not $blocked -or (Order) -ne '1,3,2,4'){throw 'Reordering was not blocked during playback.'}
    $motion.Stop()
    'PASS: row drag/drop, insertion order, selected waypoint, unchanged poses, XML order and playback guard; no game access.'
} finally {$window.Dispose()}


