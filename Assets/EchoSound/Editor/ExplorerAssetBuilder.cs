using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoSound.Editor
{
    /// <summary>Reproducible, original mesh and animation source. No downloaded game assets.</summary>
    public static class ExplorerAssetBuilder
    {
        private const string Art = "Assets/EchoSound/Art/Explorer";
        private const string Prefab = "Assets/EchoSound/Prefabs/Player/Explorer.prefab";
        private static readonly string[] BoneNames = { "Hips", "Spine", "Chest", "Head",
            "UpperArm.L", "Forearm.L", "Hand.L", "UpperArm.R", "Forearm.R", "Hand.R",
            "Thigh.L", "Shin.L", "Foot.L", "Thigh.R", "Shin.R", "Foot.R" };
        private static Transform[] bones;
        private static Material[] materials;

        [MenuItem("EchoSound/Player/Rebuild explorer assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Art + "/Animations");
            AssetDatabase.Refresh();
            materials = new[] {
                Material("Suit • petrol", new Color(.12f,.27f,.28f), .05f, .34f),
                Material("Rubber • graphite", new Color(.035f,.048f,.055f), 0,.25f),
                Material("Trim • ceramic", new Color(.72f,.78f,.73f), .25f,.45f),
                Material("Harness • ochre", new Color(.9f,.39f,.09f), .05f,.28f),
                Material("Hardware • titanium", new Color(.23f,.31f,.34f), .7f,.55f),
                Material("Lens • smoked glass", new Color(.018f,.08f,.09f), .65f,.85f),
                Material("Markings • seafoam", new Color(.22f,.86f,.72f), .25f,.5f)
            };
            var root = new GameObject("Explorer");
            try
            {
                bones = new Transform[BoneNames.Length];
                Bone(0, root.transform, new Vector3(0,.83f,0));
                Bone(1,bones[0],new Vector3(0,1.01f,0));
                Bone(2,bones[1],new Vector3(0,1.22f,0));
                Bone(3,bones[2],new Vector3(0,1.47f,0));
                for (int side=0; side<2; side++)
                {
                    float s = side==0 ? -1 : 1;
                    int a=4+side*3, l=10+side*3;
                    Bone(a,bones[2],new Vector3(s*.29f,1.25f,0));
                    Bone(a+1,bones[a],new Vector3(s*.36f,.98f,0));
                    Bone(a+2,bones[a+1],new Vector3(s*.39f,.74f,.025f));
                    Bone(l,bones[0],new Vector3(s*.135f,.80f,0));
                    Bone(l+1,bones[l],new Vector3(s*.14f,.44f,.01f));
                    Bone(l+2,bones[l+1],new Vector3(s*.14f,.14f,.025f));
                }
                var body = new Geometry();
                var helmet = new Geometry();
                // Tailored pressure suit: barrel chest, articulated limbs, layered seams.
                body.Shape(new Vector3(0,1.12f,0),new Vector3(.48f,.52f,.32f),0,1,.65f);
                body.Shape(new Vector3(0,.85f,0),new Vector3(.4f,.24f,.30f),0,0,.55f);
                body.Shape(new Vector3(0,.94f,0),new Vector3(.43f,.08f,.34f),1,0,.35f);
                body.Shape(new Vector3(0,.95f,.181f),new Vector3(.09f,.07f,.025f),4,0,.25f);
                body.Shape(new Vector3(0,1.32f,0),new Vector3(.3f,.12f,.29f),1,2,.5f);
                body.Shape(new Vector3(0,1.36f,0),new Vector3(.32f,.045f,.31f),2,2,.5f);
                // Backpack, regulator tanks and shoulder straps.
                body.Shape(new Vector3(0,1.11f,-.23f),new Vector3(.34f,.44f,.18f),1,2,.3f);
                body.Shape(new Vector3(0,1.15f,-.33f),new Vector3(.27f,.28f,.035f),0,2,.35f);
                for (int side=0;side<2;side++)
                {
                    float s=side==0?-1:1; int a=4+side*3, l=10+side*3;
                    body.Shape(new Vector3(s*.14f,1.15f,.166f),new Vector3(.052f,.34f,.037f),3,2,.3f);
                    body.Shape(new Vector3(s*.14f,1.04f,.196f),new Vector3(.068f,.064f,.025f),4,2,.25f);
                    body.Shape(new Vector3(s*.215f,1.1f,-.25f),new Vector3(.105f,.37f,.13f),4,2,.85f);
                    body.Shape(new Vector3(s*.215f,.96f,-.25f),new Vector3(.12f,.06f,.14f),3,2,.4f);
                    body.Shape(new Vector3(s*.295f,1.24f,0),new Vector3(.23f,.20f,.25f),0,a,.8f);
                    body.Shape(new Vector3(s*.326f,1.19f,.012f),new Vector3(.23f,.09f,.26f),2,a,.6f);
                    body.Shape(new Vector3(s*.335f,1.08f,0),new Vector3(.185f,.26f,.19f),0,a,.75f);
                    body.Shape(new Vector3(s*.36f,.98f,0),new Vector3(.166f,.105f,.18f),1,a+1,.75f);
                    body.Shape(new Vector3(s*.377f,.865f,.012f),new Vector3(.17f,.23f,.19f),0,a+1,.65f);
                    body.Shape(new Vector3(s*.385f,.77f,.018f),new Vector3(.19f,.07f,.20f),3,a+1,.4f);
                    body.Shape(new Vector3(s*.398f,.68f,.035f),new Vector3(.15f,.18f,.16f),1,a+2,.65f);
                    body.Shape(new Vector3(s*.348f,.70f,.092f),new Vector3(.065f,.105f,.08f),1,a+2,.8f);
                    body.Shape(new Vector3(s*.40f,.70f,.112f),new Vector3(.10f,.07f,.024f),4,a+2,.3f);
                    body.Shape(new Vector3(s*.135f,.64f,0),new Vector3(.22f,.34f,.25f),0,l,.7f);
                    body.Shape(new Vector3(s*.22f,.68f,.02f),new Vector3(.10f,.17f,.21f),3,l,.3f);
                    body.Shape(new Vector3(s*.14f,.44f,.035f),new Vector3(.20f,.14f,.25f),1,l+1,.75f);
                    body.Shape(new Vector3(s*.14f,.46f,.153f),new Vector3(.155f,.15f,.05f),4,l+1,.4f);
                    body.Shape(new Vector3(s*.14f,.29f,.01f),new Vector3(.19f,.24f,.22f),0,l+1,.6f);
                    body.Shape(new Vector3(s*.14f,.19f,.01f),new Vector3(.205f,.06f,.23f),2,l+1,.35f);
                    body.Shape(new Vector3(s*.14f,.105f,.06f),new Vector3(.235f,.19f,.37f),1,l+2,.4f);
                    body.Shape(new Vector3(s*.14f,.035f,.075f),new Vector3(.25f,.06f,.39f),4,l+2,.25f);
                    body.Shape(new Vector3(s*.14f,.13f,.18f),new Vector3(.22f,.08f,.13f),4,l+2,.45f);
                    // Raised boot tread stays readable under the echo contour shader.
                    for(int t=0;t<3;t++)
                        body.Shape(new Vector3(s*.14f,.032f,-.045f+t*.105f),new Vector3(.26f,.035f,.026f),1,l+2,.3f);
                }
                body.Shape(new Vector3(0,1.18f,.184f),new Vector3(.16f,.16f,.05f),1,2,.3f);
                body.Shape(new Vector3(0,1.205f,.215f),new Vector3(.12f,.048f,.015f),2,2,.3f);
                for(int i=0;i<3;i++) body.Shape(new Vector3(-.034f+i*.034f,1.15f,.218f),new Vector3(.018f,.04f,.012f),6,2,.4f);
                // Large closed helmet, binocular rings and a triangular respirator silhouette.
                helmet.Shape(new Vector3(0,1.51f,0),new Vector3(.56f,.54f,.48f),0,3,.8f);
                helmet.Shape(new Vector3(0,1.54f,.19f),new Vector3(.49f,.30f,.16f),1,3,.55f);
                helmet.Shape(new Vector3(0,1.75f,.005f),new Vector3(.075f,.052f,.34f),3,3,.5f);
                for(int side=0;side<2;side++)
                {
                    float s=side==0?-1:1;
                    helmet.Shape(new Vector3(s*.28f,1.53f,0),new Vector3(.09f,.23f,.22f),1,3,.45f);
                    helmet.Shape(new Vector3(s*.325f,1.53f,.005f),new Vector3(.025f,.16f,.14f),4,3,.35f);
                    helmet.Shape(new Vector3(s*.341f,1.55f,.005f),new Vector3(.015f,.075f,.04f),6,3,.4f);
                    helmet.Ring(new Vector3(s*.126f,1.555f,.27f),.112f,.023f,4,3);
                    helmet.Shape(new Vector3(s*.126f,1.555f,.279f),new Vector3(.174f,.174f,.042f),5,3,1);
                    helmet.Ring(new Vector3(s*.126f,1.555f,.303f),.078f,.007f,6,3);
                    helmet.Ring(new Vector3(s*.126f,1.555f,.305f),.052f,.004f,2,3);
                    helmet.Shape(new Vector3(s*.126f-.025f,1.585f,.31f),new Vector3(.023f,.039f,.008f),2,3,.8f);
                    helmet.Shape(new Vector3(s*.092f,1.355f,.235f),new Vector3(.11f,.11f,.13f),4,3,.7f);
                    helmet.Ring(new Vector3(s*.092f,1.355f,.30f),.043f,.009f,1,3);
                }
                helmet.Shape(new Vector3(0,1.395f,.247f),new Vector3(.17f,.17f,.13f),1,3,.65f);
                helmet.Shape(new Vector3(0,1.405f,.315f),new Vector3(.11f,.10f,.03f),4,3,.5f);
                for(int i=0;i<3;i++) helmet.Shape(new Vector3(0,1.38f+i*.025f,.334f),new Vector3(.073f,.009f,.012f),1,3,.3f);
                var bodyRenderer = Skin(root, "Suit", body);
                var headRenderer = Skin(root, "Helmet", helmet);
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildController(root.transform, false);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var avatar = root.AddComponent<ExplorerAvatar>();
                avatar.animator=animator; avatar.headRenderer=headRenderer; avatar.head=bones[3];
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
                InstallOnPlayer();
                BuildShowcase(root);
                AssetDatabase.SaveAssets();
                Debug.Log("Explorer: meshes, rig, 6 clips, controllers, player integration and showcase created.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Bone(int index, Transform parent, Vector3 position)
        {
            bones[index] = new GameObject(BoneNames[index]).transform;
            bones[index].SetParent(parent,false);
            bones[index].position=position;
        }

        private static Material Material(string name, Color color, float metal, float smooth)
        {
            var m = new Material(Shader.Find("Standard")) { name=name, color=color };
            m.SetFloat("_Metallic",metal); m.SetFloat("_Glossiness",smooth);
            return Save(m,Art+"/"+name.Split(' ')[0]+".mat");
        }

        private static T Save<T>(T asset,string path) where T:UnityEngine.Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old!=null) { EditorUtility.CopySerialized(asset,old); UnityEngine.Object.DestroyImmediate(asset); EditorUtility.SetDirty(old); return old; }
            AssetDatabase.CreateAsset(asset,path); return asset;
        }

        private static SkinnedMeshRenderer Skin(GameObject root,string name,Geometry geometry)
        {
            var go=new GameObject(name); go.transform.SetParent(root.transform,false);
            var mesh=geometry.Mesh(name);
            var poses=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++) poses[i]=bones[i].worldToLocalMatrix*root.transform.localToWorldMatrix;
            mesh.bindposes=poses;
            mesh=Save(mesh,Art+"/"+name+".asset");
            var renderer=go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=mesh; renderer.sharedMaterials=materials; renderer.bones=bones; renderer.rootBone=bones[0];
            renderer.localBounds=new Bounds(new Vector3(0,.95f,0),new Vector3(2,2.6f,2));
            renderer.updateWhenOffscreen=true;
            return renderer;
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<Vector2> uvs=new List<Vector2>();
            private readonly List<BoneWeight> weights=new List<BoneWeight>();
            private readonly List<int>[] faces=new List<int>[7];
            public Geometry() { for(int i=0;i<faces.Length;i++) faces[i]=new List<int>(); }
            private static float P(float value,float power) => Mathf.Sign(value)*Mathf.Pow(Mathf.Abs(value),power);
            public void Shape(Vector3 center,Vector3 size,int material,int bone,float round)
            {
                const int n=16, m=10; int first=vertices.Count;
                for(int y=0;y<=m;y++)
                {
                    float lat=-Mathf.PI/2+Mathf.PI*y/m;
                    for(int x=0;x<=n;x++)
                    {
                        float lon=2*Mathf.PI*x/n;
                        Vector3 p=new Vector3(P(Mathf.Cos(lat),round)*P(Mathf.Cos(lon),round),P(Mathf.Sin(lat),round),P(Mathf.Cos(lat),round)*P(Mathf.Sin(lon),round));
                        Add(center+Vector3.Scale(p,size)*.5f,new Vector2((float)x/n,(float)y/m),bone);
                    }
                }
                Grid(first,n,m,material);
            }
            public void Ring(Vector3 center,float radius,float tube,int material,int bone)
            {
                const int n=32,m=8; int first=vertices.Count;
                for(int y=0;y<=m;y++) for(int x=0;x<=n;x++)
                {
                    float a=2*Mathf.PI*x/n,b=-2*Mathf.PI*y/m,r=radius+tube*Mathf.Cos(b);
                    Add(center+new Vector3(r*Mathf.Cos(a),r*Mathf.Sin(a),tube*Mathf.Sin(b)),new Vector2((float)x/n,(float)y/m),bone);
                }
                Grid(first,n,m,material);
            }
            private void Add(Vector3 p,Vector2 uv,int bone)
            { vertices.Add(p); uvs.Add(uv); weights.Add(new BoneWeight { boneIndex0=bone,weight0=1 }); }
            private void Grid(int first,int n,int m,int material)
            {
                for(int y=0;y<m;y++) for(int x=0;x<n;x++)
                {
                    int a=first+y*(n+1)+x,b=a+n+1;
                    faces[material].AddRange(new[]{a,b,a+1,a+1,b,b+1});
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh { name=name,indexFormat=IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.boneWeights=weights.ToArray(); mesh.subMeshCount=faces.Length;
                for(int i=0;i<faces.Length;i++) mesh.SetTriangles(faces[i],i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }

        private static AnimatorController BuildController(Transform root,bool preview)
        {
            string[] names={"Idle","Walk","Run","Jump","Fall","Land"};
            var clips=new AnimationClip[6];
            for(int i=0;i<6;i++) clips[i]=Clip(root,names[i]);
            string path=Art+(preview?"/ExplorerPreview.controller":"/Explorer.controller");
            // Rebuild subassets in place, retaining the main controller GUID for prefab references.
            var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(existing!=null)
            {
                existing.layers=Array.Empty<AnimatorControllerLayer>();
                existing.parameters=Array.Empty<AnimatorControllerParameter>();
                foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(path)) if(sub!=existing) UnityEngine.Object.DestroyImmediate(sub,true);
            }
            var controller=existing!=null?existing:AnimatorController.CreateAnimatorControllerAtPath(path);
            if(controller.layers.Length==0) controller.AddLayer("Base Layer");
            var sm=controller.layers[0].stateMachine;
            if(preview)
            {
                for(int i=0;i<6;i++) { var state=sm.AddState(names[i]); state.motion=clips[i]; if(i==0) sm.defaultState=state; }
                return controller;
            }
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded",AnimatorControllerParameterType.Bool);
            controller.AddParameter("Rising",AnimatorControllerParameterType.Bool);
            controller.AddParameter("Land",AnimatorControllerParameterType.Trigger);
            var parameters=controller.parameters; parameters[1].defaultBool=true; controller.parameters=parameters;
            var locomotion=sm.AddState("Locomotion");
            var tree=new BlendTree { name="Idle / Walk / Run",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false };
            AssetDatabase.AddObjectToAsset(tree,controller);
            tree.AddChild(clips[0],0); tree.AddChild(clips[1],4.5f); tree.AddChild(clips[2],7.5f);
            locomotion.motion=tree; sm.defaultState=locomotion;
            var jump=sm.AddState("Jump"); jump.motion=clips[3];
            var fall=sm.AddState("Fall"); fall.motion=clips[4];
            var land=sm.AddState("Land"); land.motion=clips[5];
            Transition(locomotion,jump,"Grounded",AnimatorConditionMode.IfNot,"Rising",AnimatorConditionMode.If);
            Transition(locomotion,fall,"Grounded",AnimatorConditionMode.IfNot,"Rising",AnimatorConditionMode.IfNot);
            Transition(jump,fall,"Rising",AnimatorConditionMode.IfNot);
            Transition(jump,land,"Grounded",AnimatorConditionMode.If);
            Transition(fall,land,"Grounded",AnimatorConditionMode.If);
            Transition(locomotion,land,"Land",AnimatorConditionMode.If);
            var recover=land.AddTransition(locomotion); recover.hasExitTime=true; recover.exitTime=.85f; recover.duration=.1f;
            Transition(land,jump,"Grounded",AnimatorConditionMode.IfNot,"Rising",AnimatorConditionMode.If);
            Transition(land,fall,"Grounded",AnimatorConditionMode.IfNot,"Rising",AnimatorConditionMode.IfNot);
            return controller;
        }

        private static void Transition(AnimatorState from,AnimatorState to,string parameter,AnimatorConditionMode mode,string second=null,AnimatorConditionMode secondMode=AnimatorConditionMode.If)
        {
            var transition=from.AddTransition(to); transition.hasExitTime=false; transition.duration=.10f;
            transition.AddCondition(mode,0,parameter); if(second!=null) transition.AddCondition(secondMode,0,second);
        }

        private static AnimationClip Clip(Transform root,string name)
        {
            float duration=name=="Idle"?3:name=="Walk"?.88f:name=="Run"?.64f:name=="Land"?.38f:.7f;
            bool loop=name=="Idle"||name=="Walk"||name=="Run"||name=="Fall";
            var clip=new AnimationClip { name=name,frameRate=30 };
            for(int b=0;b<bones.Length;b++)
            {
                string path=AnimationUtility.CalculateTransformPath(bones[b],root);
                var rotations=new AnimationCurve[] { new AnimationCurve(),new AnimationCurve(),new AnimationCurve() };
                var height=new AnimationCurve();
                for(int frame=0;frame<=60;frame++)
                {
                    float t=frame/60f,phase=t*Mathf.PI*2;
                    Vector3 r=Vector3.zero; float bob=0;
                    bool run=name=="Run", walk=name=="Walk";
                    if(walk||run)
                    {
                        float swing=Mathf.Sin(phase), amplitude=run?42:27;
                        if(b==0) { r=new Vector3(0,Mathf.Sin(phase)*3,Mathf.Sin(phase)*(run?4:2)); bob=(1-Mathf.Cos(phase*2))*(run?.024f:.012f); }
                        if(b==1) r.x=run?9:3;
                        if(b==2) r.y=-swing*5;
                        if(b==3) r.x=run?-6:-2;
                        if(b==4||b==7) r=new Vector3((b==4?-1:1)*swing*amplitude*.8f,0,b==4?8:-8);
                        if(b==5||b==8) r.x=(run?-65:-18)-Mathf.Max(0,(b==5?-1:1)*swing)*15;
                        if(b==10||b==13) r.x=(b==10?1:-1)*swing*amplitude;
                        if(b==11||b==14) r.x=Mathf.Max(0,(b==11?1:-1)*swing)*(run?75:48);
                        if(b==12||b==15) r.x=-Mathf.Max(0,(b==12?1:-1)*swing)*18;
                    }
                    else if(name=="Jump"||name=="Fall")
                    {
                        bool up=name=="Jump"; float ease=up?Mathf.Sin(t*Mathf.PI*.5f):1;
                        if(b==1) r.x=up?-7:8;
                        if(b==4||b==7) r=new Vector3(up?-24:-12,0,(b==4?1:-1)*(up?22:32));
                        if(b==5||b==8) r.x=-38;
                        if(b==10||b==13) r.x=(b==10?-26:-12)*ease;
                        if(b==11||b==14) r.x=(up?48:27)*ease;
                        if(b==12||b==15) r.x=up?16:10;
                    }
                    else if(name=="Land")
                    {
                        float squash=Mathf.Sin(Mathf.PI*t)*(1-t);
                        if(b==0) bob=-.16f*squash;
                        if(b==1) r.x=20*squash;
                        if(b==10||b==13) r.x=-40*squash;
                        if(b==11||b==14) r.x=75*squash;
                        if(b==12||b==15) r.x=-35*squash;
                        if(b==4||b==7) r.x=-20*squash;
                    }
                    else
                    {
                        if(b==0) bob=Mathf.Sin(phase)*.006f;
                        if(b==1) r.x=Mathf.Sin(phase)*1.5f;
                        if(b==3) r=new Vector3(-Mathf.Sin(phase)*1.5f,Mathf.Sin(phase)*2,0);
                        if(b==4||b==7) r=new Vector3(Mathf.Sin(phase)*2,0,b==4?5:-5);
                        if(b==5||b==8) r.x=-10;
                    }
                    rotations[0].AddKey(t*duration,r.x); rotations[1].AddKey(t*duration,r.y); rotations[2].AddKey(t*duration,r.z);
                    height.AddKey(t*duration,bones[b].localPosition.y+bob);
                }
                for(int axis=0;axis<3;axis++) clip.SetCurve(path,typeof(Transform),"localEulerAnglesRaw."+"xyz"[axis],rotations[axis]);
                if(b==0) clip.SetCurve(path,typeof(Transform),"m_LocalPosition.y",height);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop; settings.loopBlend=loop;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            return Save(clip,Art+"/Animations/"+name+".anim");
        }

        private static void InstallOnPlayer()
        {
            const string path="Assets/EchoSound/Prefabs/Player/FirstPersonPlayer.prefab";
            var player=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old=player.transform.Find("Explorer"); if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),player.transform);
                model.GetComponent<ExplorerAvatar>().ownerCamera=player.GetComponent<FirstPersonPlayer>().view;
                PrefabUtility.SaveAsPrefabAsset(player,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        private static void BuildShowcase(GameObject source)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var root=new GameObject("Explorer showcase"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var model=UnityEngine.Object.Instantiate(source,root.transform); model.name="Explorer";
            model.GetComponent<ExplorerAvatar>().driveFromPlayer=false;
            model.GetComponent<Animator>().runtimeAnimatorController=BuildController(source.transform,true);
            model.transform.rotation=Quaternion.Euler(0,-18,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cylinder); floor.name="Display plinth"; floor.transform.SetParent(root.transform,false);
            floor.transform.localPosition=new Vector3(0,-.075f,0); floor.transform.localScale=new Vector3(2.5f,.075f,2.5f);
            floor.GetComponent<Renderer>().sharedMaterial=materials[1];
            var camera=new GameObject("Showcase camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform,false);
            camera.transform.position=new Vector3(2.6f,1.75f,4.2f); camera.transform.LookAt(new Vector3(0,.91f,0));
            camera.fieldOfView=30; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.052f,.065f);
            camera.nearClipPlane=.05f;
            Light(root.transform,"Key",new Vector3(35,-35,0),new Color(1,.86f,.7f),1.6f);
            Light(root.transform,"Fill",new Vector3(15,145,0),new Color(.38f,.72f,1),1.2f);
            Light(root.transform,"Rim",new Vector3(15,220,0),new Color(.42f,1,.8f),1.5f);
            var demo=root.AddComponent<ExplorerShowcase>(); demo.animator=model.GetComponent<Animator>(); demo.model=model.transform;
            EditorSceneManager.SaveScene(scene,"Assets/EchoSound/Scenes/PlayerShowcase.unity");
            RenderPreview(camera,model);
            EditorSceneManager.CloseScene(scene,true);
        }

        private static void Light(Transform parent,string name,Vector3 angles,Color color,float intensity)
        {
            var light=new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent,false);
            light.type=LightType.Directional; light.transform.rotation=Quaternion.Euler(angles); light.color=color; light.intensity=intensity;
        }

        private static void RenderPreview(Camera camera,GameObject model)
        {
            Directory.CreateDirectory("docs/player");
            var rt=new RenderTexture(1000,1000,24); var image=new Texture2D(1000,1000,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Art+"/Animations/Idle.anim"); clip.SampleAnimation(model,.5f);
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1000,1000),0,0); image.Apply(); File.WriteAllBytes("docs/player/explorer-preview.png",image.EncodeToPNG());
            }
            finally { camera.targetTexture=null; RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
        }

        [MenuItem("EchoSound/Open player showcase")]
        public static void OpenShowcase()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/EchoSound/Scenes/PlayerShowcase.unity");
        }
    }
}
