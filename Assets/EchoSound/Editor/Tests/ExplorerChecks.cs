using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EchoSound.Editor.Tests
{
    public static class ExplorerChecks
    {
        [MenuItem("EchoSound/Checks/Validate explorer avatar")]
        public static void Validate()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EchoSound/Prefabs/Player/Explorer.prefab");
            Require(prefab!=null,"Missing explorer prefab.");
            var instance=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var avatar=instance.GetComponent<ExplorerAvatar>();
                Require(avatar!=null&&avatar.animator!=null&&avatar.head!=null&&avatar.headRenderer!=null,"Rig references must be assigned.");
                Require(instance.GetComponentsInChildren<Collider>().Length==0,"The visual rig must not add player collisions.");
                int triangles=0;
                foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=skin.sharedMesh;
                    Require(mesh!=null&&mesh.vertexCount>0,"Skinned mesh must be persisted.");
                    Require(mesh.bindposes.Length==skin.bones.Length&&skin.bones.Length==16,"All 16 rig bones need bind poses.");
                    Require(mesh.boneWeights.Length==mesh.vertexCount,"Every vertex needs weights.");
                    Require(mesh.subMeshCount==skin.sharedMaterials.Length,"Every submesh needs a material.");
                    foreach(var weight in mesh.boneWeights)
                        Require(weight.boneIndex0<skin.bones.Length&&Mathf.Approximately(weight.weight0,1),"Rigid suit panels need valid normalized weights.");
                    foreach(var material in skin.sharedMaterials) Require(material!=null&&material.shader!=null,"Material/shader is missing.");
                    triangles+=mesh.triangles.Length/3;
                }
                foreach(string name in new[]{"Idle","Walk","Run","Jump","Fall","Land"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/EchoSound/Art/Explorer/Animations/"+name+".anim");
                    Require(clip!=null&&clip.length>0,"Missing or empty "+name+" clip.");
                    foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                        Require(instance.transform.Find(binding.path)!=null,"Unresolved animation bone: "+binding.path);
                    Require(!clip.legacy,"Clips must be compatible with Animator.");
                }
                var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/EchoSound/Art/Explorer/Animations/Walk.anim");
                var foot=instance.transform.Find("Hips/Thigh.L/Shin.L/Foot.L");
                walk.SampleAnimation(instance,0); var a=foot.position;
                walk.SampleAnimation(instance,walk.length*.25f);
                Require(Vector3.Distance(a,foot.position)>.08f,"Walk must articulate the leg chain.");
                walk.SampleAnimation(instance,walk.length);
                Require(Vector3.Distance(a,foot.position)<.001f,"Walk loop must return to its start pose.");
                var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EchoSound/Prefabs/Player/FirstPersonPlayer.prefab");
                var integrated=player.GetComponentInChildren<ExplorerAvatar>();
                Require(integrated!=null&&integrated.ownerCamera==player.GetComponent<FirstPersonPlayer>().view,"Player prefab needs the model and its owner camera.");
                Require(!avatar.animator.applyRootMotion,"Controller owns movement; animation must remain in place.");
                var controller=avatar.animator.runtimeAnimatorController as AnimatorController;
                Require(controller!=null&&controller.layers[0].stateMachine.defaultState.name=="Locomotion","Locomotion must be the default state.");
                Debug.Log("Explorer checks PASSED: rig, weights, materials, 6 clip bindings, articulated seamless walk, player integration. Triangles: "+triangles);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Explorer check: " + message);
        }
    }
}
