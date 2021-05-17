module ExtendingSceneGraphs 

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.SceneGraph.IO
open FSharp.Data.Adaptive.Operators

let run () = 

    // create an OpenGL/Vulkan application. Use the use keyword (using in C#) in order to
    // properly dipose resources on shutdown...
    use app = new OpenGlApplication()
    // SimpleRenderWindow is a System.Windows.Forms.Form which contains a render control
    // of course you can a custum form and add a control to it.
    // Note that there is also a WPF binding for OpenGL. For more complex GUIs however,
    // we recommend using aardvark-media anyways..
    let win = app.CreateGameWindow(samples = 8)

    // Given eye, target and sky vector we compute our initial camera pose
    let initialView = CameraView.LookAt(V3d(3.0,3.0,3.0), V3d.Zero, V3d.OOI)
    // the class Frustum describes camera frusta, which can be used to compute a projection matrix.
    let frustum = 
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        win.Sizes 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    // create a controlled camera using the window mouse and keyboard input devices
    // the window also provides a so called time mod, which serves as tick signal to create
    // animations
    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView
    
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

    let spheres = 
        [
            for x in -3.0 .. 3.0 do
                for y in -3.0 .. 3.0 do
                    yield 
                        Sg.sphere' 4 C4b.White 0.1
                        |> Sg.translate x y 0.0
        ] |> Sg.ofSeq

    let lodDecider (threshhold : float) (scope : LodScope) =
        (scope.bb.Center - scope.cameraPosition).Length < threshhold

    let manyObjects =
        [
            for x in -3.0 .. 3.0 do
                for y in -3.0 .. 3.0 do
                        let highDetail = Sg.lod (lodDecider 2.0) (Sg.unitSphere 3 ~~C4b.Red) aardvark
                        yield 
                            Sg.lod (lodDecider 5.0) (Sg.box ~~C4b.Red ~~(Box3d.FromCenterAndSize(V3d.OOO,V3d.III*2.0))) highDetail
                            |> Sg.diffuseTexture DefaultTextures.checkerboard
                            |> Sg.scale 0.4
                            |> Sg.translate x y 0.0
        ] |> Sg.ofSeq

    let scene = aardvark
    //let scene = spheres
    //let scene = manyObjects

    let sg =
        scene
            // here we use fshade to construct a shader: https://github.com/aardvark-platform/aardvark.docs/wiki/FShadeOverview
            |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]
            |> Sg.andAlso (aardvark |> Sg.onOff (AVal.constant false))
            // extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            // compute a projection trafo, given the frustum contained in frustum
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )

    let renderTask = 
        // compile the scene graph into a render task
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    // assign the render task to our window...
    win.RenderTask <- renderTask
    win.Run()
