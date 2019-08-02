﻿#if INTERACTIVE
#load @"..\..\.paket\load\net471\main.group.fsx"
#else
#endif

#load @"..\..\paket-files\aardvark-platform\aardvark.rendering\src\Application\Aardvark.Application.Utilities\FsiHelper.fsx"
FsiHelper.InteractiveHelper.init()


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph.IO


// create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
let app = new OpenGlApplication()
// SimpleRenderWindow is a System.Windows.Forms.Form which contains a render control
// of course you can a custum form and add a control to it.
// Note that there is also a WPF binding for OpenGL. For more complex GUIs however,
// we recommend using aardvark-media anyways..
let win = app.CreateSimpleRenderWindow(samples = 8)
//win.Title <- "Hello Aardvark"

// Given eye, target and sky vector we compute our initial camera pose
let initialView = CameraView.LookAt(V3d(3.0,3.0,3.0), V3d.Zero, V3d.OOI)
// the class Frustum describes camera frusta, which can be used to compute a projection matrix.
let frustum = 
    // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
    win.Sizes 
        // construct a standard perspective frustum (60 degrees horizontal field of view,
        // near plane 0.1, far plane 50.0 and aspect ratio x/y.
        |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

// create a controlled camera using the window mouse and keyboard input devices
// the window also provides a so called time mod, which serves as tick signal to create
// animations - seealso: https://github.com/aardvark-platform/aardvark.docs/wiki/animation
let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView


let aardvark =
    let modelPath = Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; "data"; "aardvark"; "aardvark.obj" ]

    let aardvark = 
        Loader.Assimp.load modelPath
         |> Sg.adapter
         |> Sg.normalizeTo (Box3d(-V3d.III, V3d.III))
         |> Sg.transform (Trafo3d.FromOrthoNormalBasis(V3d.IOO,V3d.OIO,-V3d.OOI))
         |> Sg.shader {
               do! DefaultSurfaces.trafo
               do! DefaultSurfaces.constantColor C4f.White
               do! DefaultSurfaces.diffuseTexture
               do! DefaultSurfaces.normalMap
               do! DefaultSurfaces.simpleLighting
           }
    aardvark

let cubeLength = Mod.init 2

let currentScene = 
    cubeLength |> Mod.map (fun cnt -> 
        let cnt = max 0 cnt 
        [
            for x in 0 .. (cnt - 1) do
                for y in 0 .. (cnt - 1) do
                    for z in 0 .. (cnt - 1) do
                        yield aardvark |> Sg.translate (float x * 2.0) (float y * 2.0) (float z * 2.0)
        ] |> Sg.ofSeq
    )

let renderTask =
    currentScene
        |> Sg.dynamic
        |> Sg.effect [
                DefaultSurfaces.trafo                 |> toEffect
                DefaultSurfaces.constantColor C4f.Red |> toEffect
                DefaultSurfaces.simpleLighting        |> toEffect
            ]
        // extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
        |> Sg.viewTrafo (cameraView  |> Mod.map CameraView.viewTrafo )
        // compute a projection trafo, given the frustum contained in frustum
        |> Sg.projTrafo (frustum |> Mod.map Frustum.projTrafo    )
        |> Sg.compile win.Runtime win.FramebufferSignature


// assign the render task to our window...
win.RenderTask <- renderTask
win.Visible <- true


let change () =
    transact (fun _ -> cubeLength.Value <- min 5 (cubeLength.Value + 1))