using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biofit.Geometry.Editor
{
    // Actual Unity Rigidbody2D tests. They are prepared in the repository;
    // host tests cannot claim to have run this class without Unity Editor.
    public static class GeometryPlayValidation
    {
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play mode requerido: callbacks reales de colision/trigger.");
            int passed=0;
            foreach(string name in GeometryLevel.ResourceNames)
                foreach(float dt in new[]{.01f,.02f})
                    foreach(float offset in new[]{-.06f,0f,.06f})
                    {
                        Replay(name,dt,offset);passed++;
                    }
            Debug.Log("BIOFIT GEOMETRY: PASS "+passed+" recorridos completos con Rigidbody2D/colliders reales. NO es BLE ni Android fisico.");
        }
        private static void Replay(string name,float dt,float offset)
        {
            var scene=SceneManager.CreateScene("GeometryValidation_"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var root=new GameObject("GeometryValidation");SceneManager.MoveGameObjectToScene(root,scene);
            var square=GeometryWorld.CreateSprite(0);var triangle=GeometryWorld.CreateSprite(1);var circle=GeometryWorld.CreateSprite(2);
            var material=new PhysicsMaterial2D("Validation"){friction=0,bounciness=0};
            try
            {
                var level=JsonUtility.FromJson<GeometryLevel>(Resources.Load<TextAsset>("BiofitGeometry/Levels/"+name).text);
                var world=new GeometryWorld(square,triangle,circle,material);world.Build(level,root.transform,Color.cyan);
                var cube=world.Player;cube.SetRunning(true);var physics=scene.GetPhysicsScene2D();
                int section=0;double time=0,lastCommand=-100;
                while(time<180 && cube.Alive && cube.Body.position.x<level.finishX)
                {
                    if(section<level.sections.Length && cube.Body.position.x>=level.sections[section].jumpX+offset*GeometryPhysics.Speed)
                    {
                        if(!level.sections[section].automaticPad)
                        {
                            if(time-lastCommand<.9)throw new Exception("Cadencia imposible: "+name);
                            if(cube.TryJump()!=JumpOutcome.Executed)throw new Exception("Salto terrestre no disponible: "+name+" tramo="+section);
                            lastCommand=time;
                        }
                        section++;
                    }
                    cube.StepPhysics(dt);
                    if(!physics.Simulate(dt))throw new Exception("Simulacion no ejecutada");
                    time+=dt;
                }
                if(!cube.Alive || cube.Body.position.x<level.finishX)throw new Exception("Recorrido NO superado: "+name+" dt="+dt+" offset="+offset+" x="+cube.Body.position.x);
            }
            finally
            {
                root.SetActive(false);UnityEngine.Object.Destroy(root);SceneManager.UnloadSceneAsync(scene);
                foreach(var s in new[]{square,triangle,circle}){UnityEngine.Object.Destroy(s.texture);UnityEngine.Object.Destroy(s);}
                UnityEngine.Object.Destroy(material);
            }
        }
    }
}
