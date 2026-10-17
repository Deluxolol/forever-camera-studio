$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field([string]$name){,$type.GetField($name,$flags).GetValue($window)}
function Route {,$type.GetMethod('PlaybackRoute',$flags).Invoke($window,@())}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $points=Field 'points';$grid=Field 'grid';$selection=Field 'startFromSelected';$motion=Field 'motion'
    foreach($i in 0..5){$p=[Machinae.Forever.CameraPoint]::new();$p.X=$i*10;$p.Y=$i*2;$p.Z=50;$points.Add($p)}
    $grid.CurrentCell=$grid.Rows[2].Cells[0]
    if((Route).Count -ne 6){throw 'Normal PLAY did not retain the full route.'}
    $selection.Checked=$true;$route=Route
    if($route.Count -ne 4 -or $route[0].X -ne 20 -or $route[3].X -ne 50){throw 'Selected start did not choose the remaining waypoints.'}
    foreach($smooth in @($false,$true)){
        $motion.Start($route,10,$false,$smooth)
        if($motion.Advance(0,$points[0]).X -ne 20){throw 'Playback did not start at the selected waypoint.'}
        $middle=$motion.Advance(5,$points[0]);if($middle.X -le 20 -or $middle.X -ge 50){throw 'Selected section did not move.'}
        if($motion.Advance(5,$points[0]).X -ne 50 -or $motion.Playing){throw 'Selected section did not end at the final waypoint.'}
    }
    $motion.Start($route,10,$true,$false)
    if($motion.Advance(10,$points[0]).X -ne 20){throw 'Loop did not return to the selected starting point.'}
    $type.GetMethod('SyncMode',$flags).Invoke($window,@()) | Out-Null
    if($selection.Enabled){throw 'Start option remained editable during playback.'}
    $motion.Stop();$type.GetMethod('SyncMode',$flags).Invoke($window,@()) | Out-Null
    if(-not $selection.Enabled){throw 'Start option did not re-enable after stopping.'}
    $grid.CurrentCell=$grid.Rows[5].Cells[0]
    $rejected=$false;try{Route | Out-Null}catch{$rejected=$_.Exception.InnerException -is [InvalidOperationException]}
    if(-not $rejected){throw 'Selecting only the last waypoint was not explained/rejected.'}
    if($points.Count -ne 6 -or $points[0].X -ne 0){throw 'Selection changed the saved route.'}
    $grid.CurrentCell=$grid.Rows[1].Cells[0]
    $type.GetField('draggedWaypoint',$flags).SetValue($window,$points[1])
    $type.GetField('waypointDragToken',$flags).SetValue($window,'preview')
    $type.GetField('waypointDropSlot',$flags).SetValue($window,4)
    [Windows.Forms.Application]::DoEvents()
    $bitmap=[Drawing.Bitmap]::new($window.Width,$window.Height)
    try{$window.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height));$bitmap.Save((Join-Path $PSScriptRoot 'studio-Waypoint-drag.png'))}finally{$bitmap.Dispose()}
    'PASS: selected/full route, smooth/straight movement, section loop, end-point validation and playback lock; drag preview rendered without game access.'
} finally {$window.Dispose()}
