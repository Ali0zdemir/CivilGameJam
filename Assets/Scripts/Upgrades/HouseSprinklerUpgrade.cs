using System;
using System.Collections.Generic;
using CivilGameJam.Disasters;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Upgrades
{
    /// <summary>A removable roof-mounted sprinkler that protects one house during the fire preview.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseSprinklerUpgrade : MonoBehaviour
    {
        [SerializeField] private Transform building;
        [SerializeField] private FireTestController fire;
        [SerializeField] private Material metalMaterial;
        [SerializeField] private Material darkMaterial;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material nozzleMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material sprayMaterial;
        [SerializeField] private Material installationDustMaterial;
        [Header("Sprinkler")]
        [SerializeField, Range(0.15f, 0.4f)] private float mastHeight = 0.23f;
        [SerializeField, Range(0f, 1f)] private float fireIntensityMultiplier = 0.25f;
        [SerializeField, Range(10f, 90f)] private float rotationSpeed = 38f;

        private GameObject sprinklerVisuals;
        private GameObject waterVisuals;
        private Transform rotor;
        private Vector3 roofAnchor;
        private Mesh cylinderMesh;
        private Mesh jointMesh;
        private Mesh jetMesh;
        private Mesh dustMesh;
        private ParticleSystem droplets;
        private ParticleSystem roofRunoff;
        private HouseWallRunoff wallRunoff;
        private ParticleSystem installationDust;
        private Renderer groundRenderer;
        private float sprayAge;
        private float dropRemainder;
        private float runoffRemainder;
        private int dropIndex;
        private int runoffIndex;
        private MeshRenderer jetRenderer;
        private MaterialPropertyBlock jetProperties;
        private readonly System.Random random = new System.Random();
        private const int JetCount = 4;
        private const int JetSegments = 24;
        private const int JetRingSize = 6;
        private const float JetSpeed = 1.35f;
        private const float WaterGravityMultiplier = 0.4f;
        private readonly Vector3[] jetVertices = new Vector3[JetCount * (JetSegments + 1) * JetRingSize];
        private readonly Color[] jetColors = new Color[JetCount * (JetSegments + 1) * JetRingSize];
        private readonly ParticleSystem.Particle[] waterBuffer = new ParticleSystem.Particle[512];
        private readonly float[] jetFlight = new float[JetCount];
        private readonly Vector3[] jetImpacts = new Vector3[JetCount];
        private float WaterGravity => Mathf.Max(0.1f, -Physics.gravity.y * WaterGravityMultiplier);
        private float GroundLevel => groundRenderer != null ? groundRenderer.bounds.max.y + 0.008f : building.position.y + 0.008f;

        public Transform Building => building;
        public bool IsAvailable => building != null && building.Find("Second_Floor") != null && building.Find("Roof/Roof_Ridge") != null;
        public bool IsInstalled { get; private set; }
        public bool IsSpraying { get; private set; }
        public float FireMultiplier => IsInstalled && isActiveAndEnabled ? Mathf.Clamp01(fireIntensityMultiplier) : 1f;
        public event Action<HouseSprinklerUpgrade> StateChanged;

        public void SetInstalled(bool installed)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !IsAvailable || installed == IsInstalled) return;
            if (!installed) { ResetUpgrade(); return; }
            if (metalMaterial == null || darkMaterial == null || bodyMaterial == null || nozzleMaterial == null) return;
            EnsureVisuals();
            wallRunoff?.Rebuild();
            sprinklerVisuals.SetActive(true);
            IsInstalled = true;
            PlayDust();
            StateChanged?.Invoke(this);
        }

        private void EnsureVisuals()
        {
            if (sprinklerVisuals != null) return;
            var ridge = building.Find("Roof/Roof_Ridge").GetComponent<MeshRenderer>().bounds;
            groundRenderer = GameObject.Find("Continuous_Ground")?.GetComponent<Renderer>();
            roofAnchor = building.InverseTransformPoint(new Vector3(ridge.center.x, ridge.max.y + 0.005f, ridge.center.z));
            sprinklerVisuals = new GameObject("Roof_Sprinkler");
            sprinklerVisuals.transform.SetParent(building, false);
            sprinklerVisuals.transform.localPosition = roofAnchor;
            var scale = building.lossyScale;
            sprinklerVisuals.transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
            cylinderMesh = CreateCylinder(); jointMesh = CreateOctahedron();
            Tube(sprinklerVisuals.transform,"Roof_Mount",new Vector3(0f,.0125f,0f),Vector3.up,.025f,.085f,darkMaterial);
            Tube(sprinklerVisuals.transform,"Mounting_Rod",new Vector3(0f,.025f+mastHeight*.5f,0f),Vector3.up,mastHeight,.024f,metalMaterial);
            Tube(sprinklerVisuals.transform,"Lower_Collar",new Vector3(0f,.05f,0f),Vector3.up,.028f,.036f,bodyMaterial);
            Tube(sprinklerVisuals.transform,"Upper_Collar",new Vector3(0f,mastHeight+.015f,0f),Vector3.up,.025f,.038f,nozzleMaterial);
            rotor = new GameObject("Rotating_Sprinkler_Head").transform;
            rotor.SetParent(sprinklerVisuals.transform,false); rotor.localPosition = Vector3.up * (mastHeight+.04f);
            rotor.localRotation = Quaternion.Euler(0f,25f,0f);
            Part(rotor,"Red_Gimbal",jointMesh,bodyMaterial,Vector3.zero,Vector3.one*.13f,Quaternion.identity);
            Tube(rotor,"Cross_Arm_X",Vector3.zero,Vector3.right,.29f,.026f,bodyMaterial);
            Tube(rotor,"Cross_Arm_Z",Vector3.zero,Vector3.forward,.29f,.026f,bodyMaterial);
            for(var head=0;head<JetCount;head++)
            {
                var radial = JetDirection(head);
                var direction = NozzleAim(head);
                var center = radial*.187f+Vector3.up*.012f;
                Tube(rotor,"Brass_Nozzle_"+head,center,direction,.12f,.038f,nozzleMaterial);
                Tube(rotor,"Nozzle_Rim_"+head,center+direction*.064f,direction,.018f,.047f,nozzleMaterial);
                Tube(rotor,"Nozzle_Opening_"+head,center+direction*.076f,direction,.006f,.028f,darkMaterial);
            }
            EnsureWater();
        }

        private void EnsureWater()
        {
            waterVisuals = new GameObject("Sprinkler_Water"); waterVisuals.transform.SetParent(rotor,false);
            waterVisuals.SetActive(false);
            jetMesh = new Mesh {name="Low poly sprinkler jets"}; jetMesh.MarkDynamic();
            var indices = new List<int>(); var jetUvs = new Vector2[jetVertices.Length];
            for(var jet=0;jet<JetCount;jet++)
            for(var i=0;i<=JetSegments;i++)
            for(var corner=0;corner<JetRingSize;corner++)
            {
                var index = (jet*(JetSegments+1)+i)*JetRingSize+corner;
                jetColors[index]=new Color(.1f,.85f,1f,0f);
                jetUvs[index]=new Vector2((float)corner/JetRingSize,(float)i/JetSegments*.35f);
                if(i==JetSegments)continue;
                var next = (jet*(JetSegments+1)+i)*JetRingSize+(corner+1)%JetRingSize;
                indices.Add(index); indices.Add(index+JetRingSize); indices.Add(next);
                indices.Add(next); indices.Add(index+JetRingSize); indices.Add(next+JetRingSize);
            }
            jetMesh.vertices=jetVertices; jetMesh.colors=jetColors; jetMesh.uv=jetUvs; jetMesh.SetTriangles(indices,0);
            var stream = Part(waterVisuals.transform,"Water_Arcs",jetMesh,waterMaterial,Vector3.zero,Vector3.one,Quaternion.identity);
            jetRenderer=stream.GetComponent<MeshRenderer>(); jetRenderer.shadowCastingMode=ShadowCastingMode.Off;
            jetProperties = new MaterialPropertyBlock(); jetProperties.SetFloat("_SurfaceMode",0f); jetProperties.SetFloat("_Opacity",.8f);
            jetProperties.SetColor("_DeepColor",new Color(.2f,.62f,.8f)); jetProperties.SetColor("_ShallowColor",new Color(.5f,.86f,.96f));
            jetRenderer.SetPropertyBlock(jetProperties);
            droplets = CreateWaterParticles("Water_Droplets",waterVisuals.transform);
            // Runoff belongs to the house; rotating the sprinkler must not sweep drips across its walls.
            roofRunoff = CreateWaterParticles("Roof_Runoff",sprinklerVisuals.transform);
            roofRunoff.gameObject.SetActive(false);
            wallRunoff = new HouseWallRunoff(building,sprinklerVisuals.transform,waterMaterial);
        }

        private ParticleSystem CreateWaterParticles(string label,Transform parent)
        {
            var drops = new GameObject(label); drops.transform.SetParent(parent,false);
            var system = drops.AddComponent<ParticleSystem>();
            var main=system.main; main.playOnAwake=false; main.loop=true; main.useUnscaledTime=true;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=512; main.startSpeed=0f;
            main.startRotation3D=true; main.startSize3D=true; main.gravityModifier=WaterGravityMultiplier;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=system.emission; emission.enabled=false; var shape=system.shape; shape.enabled=false;
            var size=system.sizeOverLifetime; size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0f,.55f),new Keyframe(.12f,1f),new Keyframe(.92f,.85f),new Keyframe(1f,0f)));
            var renderer=system.GetComponent<ParticleSystemRenderer>(); renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=jointMesh; renderer.sharedMaterial=sprayMaterial; renderer.alignment=ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return system;
        }

        private void Update() => AdvanceSprinkler(Time.unscaledDeltaTime);

        private void AdvanceSprinkler(float deltaTime)
        {
            if(!IsInstalled || fire==null || !fire.IsRunning || waterMaterial==null || sprayMaterial==null) { StopSpray(); return; }
            if(!IsSpraying)
            {
                sprayAge=0f; waterVisuals.SetActive(true); roofRunoff.gameObject.SetActive(true);
                wallRunoff.SetActive(true);
                droplets.Play(true); roofRunoff.Play(true); IsSpraying=true;
            }
            var step=Mathf.Max(0f,deltaTime);
            sprayAge+=step;
            rotor.localRotation=Quaternion.Euler(0f,25f+sprayAge*rotationSpeed,0f);
            for(var jet=0;jet<JetCount;jet++)
            {
                var hitsRoof=FindRoofImpact(jet,out jetFlight[jet],out jetImpacts[jet]);
                wallRunoff.SetSource(jet,jetImpacts[jet],jetFlight[jet],hitsRoof);
                var flight=jetFlight[jet];
                var developedTime=Mathf.Min(sprayAge,flight);
                for(var i=0;i<=JetSegments;i++)
                {
                    var fraction=(float)i/JetSegments; var t=developedTime*fraction;
                    var point=JetPoint(jet,t);
                    var tangent=(JetPoint(jet,t+.002f)-point).normalized;
                    var across=Vector3.Cross(tangent,Vector3.up).normalized;
                    var normal=Vector3.Cross(tangent,across).normalized;
                    var progress=t/flight;
                    var ripple=.8f+.2f*Mathf.Sin((sprayAge-t)*28f+jet*2f);
                    var radius=Mathf.Lerp(.01f,.028f,Mathf.Pow(progress,1.2f))*ripple;
                    var opacity=Mathf.Lerp(.85f,.12f,progress)*ripple;
                    for(var corner=0;corner<JetRingSize;corner++)
                    {
                        var angle=corner*Mathf.PI*2f/JetRingSize;
                        var index=(jet*(JetSegments+1)+i)*JetRingSize+corner;
                        var breakup=.96f+.04f*Mathf.Sin(sprayAge*14f-progress*6f+corner*2.3f);
                        jetVertices[index]=point+(across*Mathf.Cos(angle)+normal*Mathf.Sin(angle))*radius*breakup;
                        jetColors[index]=new Color(.1f,.85f,1f,opacity);
                    }
                }
            }
            jetMesh.vertices=jetVertices; jetMesh.colors=jetColors; jetMesh.RecalculateNormals(); jetMesh.RecalculateBounds();
            jetProperties.SetFloat("_FlowTime",sprayAge); jetRenderer.SetPropertyBlock(jetProperties);
            wallRunoff.Advance(sprayAge);
            AlignAndTrimWater(droplets); AlignAndTrimWater(roofRunoff);
            dropRemainder+=step*640f;
            var count=Mathf.Min(96,Mathf.FloorToInt(dropRemainder)); dropRemainder-=count;
            for(var i=0;i<count;i++)
            {
                var head=dropIndex++%JetCount;
                var sideways=Vector3.Cross(Vector3.up,JetDirection(head));
                var position=rotor.TransformPoint(NozzleMouth(head)+sideways*Range(-.004f,.004f));
                var velocity=rotor.TransformDirection(NozzleAim(head)*JetSpeed+sideways*Range(-.12f,.12f));
                EmitWater(droplets,position,velocity,Range(.012f,.024f),Range(.025f,.06f),.7f,jetFlight[head]);
            }
            runoffRemainder+=step*480f;
            count=Mathf.Min(64,Mathf.FloorToInt(runoffRemainder)); runoffRemainder-=count;
            for(var i=0;i<count;i++)
            {
                var sample=runoffIndex++;
                if(sample%4==0)
                {
                    var head=sample/4%JetCount;
                    if(sprayAge<jetFlight[head]||!wallRunoff.TryProjectRoof(jetImpacts[head],out _,out var roofNormal))continue;
                    var velocity=roofNormal*Range(.08f,.18f)+new Vector3(Range(-.2f,.2f),.05f,Range(-.2f,.2f));
                    EmitWater(roofRunoff,jetImpacts[head]+roofNormal*.008f,velocity,Range(.012f,.024f),Range(.02f,.045f),.65f,Range(.12f,.24f));
                }
                else if(wallRunoff.TryGetDrip(sample,out var position,out var outward))
                {
                    var velocity=outward*Range(.01f,.04f)+Vector3.down*Range(.25f,.6f);
                    EmitWater(roofRunoff,position,velocity,Range(.014f,.026f),Range(.06f,.13f),.8f);
                }
            }
        }

        private static Vector3 JetDirection(int head) => Quaternion.Euler(0f,head*90f,0f)*Vector3.right;
        private static Vector3 NozzleAim(int head) => (JetDirection(head)*.3f-Vector3.up).normalized;
        private static Vector3 NozzleMouth(int head) => JetDirection(head)*.187f+Vector3.up*.012f+NozzleAim(head)*.079f;

        private Vector3 JetPoint(int head,float t)
        {
            var point=NozzleMouth(head)+NozzleAim(head)*(JetSpeed*t)-Vector3.up*(.5f*WaterGravity*t*t);
            // Older water keeps the direction it had when leaving the rotating nozzle.
            return Quaternion.Euler(0f,-rotationSpeed*t,0f)*point;
        }

        private bool FindRoofImpact(int head,out float flight,out Vector3 impact)
        {
            var previous=0f;
            for(var sample=1;sample<=32;sample++)
            {
                var time=sample*.6f/32f;
                var point=rotor.TransformPoint(JetPoint(head,time));
                if(wallRunoff.TryProjectRoof(point,out var roof,out _)&&point.y<=roof.y+.004f)
                {
                    var low=previous; var high=time;
                    for(var iteration=0;iteration<10;iteration++)
                    {
                        var middle=(low+high)*.5f;
                        var candidate=rotor.TransformPoint(JetPoint(head,middle));
                        if(wallRunoff.TryProjectRoof(candidate,out var surface,out _)&&candidate.y<=surface.y+.004f)high=middle;
                        else low=middle;
                    }
                    flight=high; impact=rotor.TransformPoint(JetPoint(head,flight));
                    return true;
                }
                previous=time;
            }
            var origin=rotor.TransformPoint(NozzleMouth(head));
            flight=FlightTime(origin.y,NozzleAim(head).y*JetSpeed);
            impact=rotor.TransformPoint(JetPoint(head,flight));
            return false;
        }

        private float FlightTime(float height,float upwardSpeed) =>
            Mathf.Max(.02f,(upwardSpeed+Mathf.Sqrt(upwardSpeed*upwardSpeed+2f*WaterGravity*Mathf.Max(0f,height-GroundLevel)))/WaterGravity);

        private void EmitWater(ParticleSystem system,Vector3 position,Vector3 velocity,float width,float length,float opacity,float lifetime=-1f)
        {
            system.Emit(new ParticleSystem.EmitParams
            {
                position=position,velocity=velocity,startLifetime=lifetime>0f?lifetime:FlightTime(position.y,velocity.y),
                startSize3D=new Vector3(width,length,width),startColor=new Color(.58f,.87f,1f,opacity),
                rotation3D=Quaternion.FromToRotation(Vector3.up,velocity.normalized).eulerAngles
            },1);
        }

        private void AlignAndTrimWater(ParticleSystem system)
        {
            var count=system.GetParticles(waterBuffer);
            var floor=GroundLevel;
            for(var i=0;i<count;i++)
            {
                if(waterBuffer[i].position.y<=floor)waterBuffer[i].remainingLifetime=0f;
                else if(system==droplets&&wallRunoff.TryProjectRoof(waterBuffer[i].position,out var roof,out _)&&waterBuffer[i].position.y<=roof.y+.003f)
                    waterBuffer[i].remainingLifetime=0f;
                else if(waterBuffer[i].velocity.sqrMagnitude>.0001f)
                    waterBuffer[i].rotation3D=Quaternion.FromToRotation(Vector3.up,waterBuffer[i].velocity.normalized).eulerAngles;
            }
            system.SetParticles(waterBuffer,count);
        }
        private float Range(float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());

        private void StopSpray()
        {
            if(!IsSpraying)return;
            if(droplets!=null)droplets.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(roofRunoff!=null){roofRunoff.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);roofRunoff.gameObject.SetActive(false);}
            if(waterVisuals!=null)waterVisuals.SetActive(false);
            wallRunoff?.SetActive(false);
            IsSpraying=false; dropRemainder=runoffRemainder=0f; sprayAge=0f;
        }

        private void PlayDust()
        {
            if(installationDustMaterial==null)return;
            if(installationDust==null)installationDust=ConstructionDustPuff.Create(transform,installationDustMaterial,out dustMesh);
            ConstructionDustPuff.Emit(installationDust,building,new Bounds(roofAnchor,new Vector3(.24f,.02f,.24f)),.45f,.13f,.5f,random,Vector3.zero);
        }

        public void ResetUpgrade()
        {
            var changed=IsInstalled; IsInstalled=false; StopSpray();
            if(sprinklerVisuals!=null)sprinklerVisuals.SetActive(false);
            if(installationDust!=null)installationDust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(changed)StateChanged?.Invoke(this);
        }

        private GameObject Part(Transform parent,string label,Mesh mesh,Material material,Vector3 position,Vector3 scale,Quaternion rotation)
        {
            var part=new GameObject(label); part.transform.SetParent(parent,false);
            part.transform.SetLocalPositionAndRotation(position,rotation); part.transform.localScale=scale;
            part.AddComponent<MeshFilter>().sharedMesh=mesh; part.AddComponent<MeshRenderer>().sharedMaterial=material;
            return part;
        }

        private void Tube(Transform parent,string label,Vector3 center,Vector3 axis,float length,float radius,Material material) =>
            Part(parent,label,cylinderMesh,material,center,new Vector3(radius*2f,length,radius*2f),Quaternion.FromToRotation(Vector3.up,axis));

        private static Mesh CreateCylinder()
        {
            var vertices=new List<Vector3>();
            for(var i=0;i<8;i++)
            {
                var a=i*Mathf.PI*.25f; var b=(i+1)*Mathf.PI*.25f;
                var p=new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f);
                var q=new Vector3(Mathf.Cos(b)*.5f,-.5f,Mathf.Sin(b)*.5f);
                vertices.AddRange(new[]{p,p+Vector3.up,q,q,p+Vector3.up,q+Vector3.up,
                    p, q, Vector3.down*.5f, p+Vector3.up,Vector3.up*.5f,q+Vector3.up});
            }
            return FlatMesh("Octagonal sprinkler pipe",vertices.ToArray());
        }

        private static Mesh CreateOctahedron()
        {
            var p=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
            var faces=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};
            var vertices=new Vector3[faces.Length]; for(var i=0;i<faces.Length;i++)vertices[i]=p[faces[i]]*.5f;
            return FlatMesh("Faceted sprinkler joint and water drop",vertices);
        }

        private static Mesh FlatMesh(string name,Vector3[] vertices)
        {
            var indices=new int[vertices.Length]; var colors=new Color32[vertices.Length];
            for(var i=0;i<indices.Length;i++){indices[i]=i;colors[i]=new Color32(255,255,255,255);}
            var mesh=new Mesh{name=name,vertices=vertices,triangles=indices,colors32=colors}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private void OnEnable() => HouseColumnUpgrade.GeometryChanged += OnHouseGeometryChanged;
        private void OnHouseGeometryChanged(HouseColumnUpgrade upgrade)
        {
            if(upgrade.Building==building)wallRunoff?.Rebuild();
        }
        private void OnDisable()
        {
            HouseColumnUpgrade.GeometryChanged -= OnHouseGeometryChanged;
            ResetUpgrade();
        }
        private void OnDestroy()
        {
            ResetUpgrade();
            wallRunoff?.Dispose();
            if(sprinklerVisuals!=null)Destroy(sprinklerVisuals);
            foreach(var mesh in new[]{cylinderMesh,jointMesh,jetMesh,dustMesh})if(mesh!=null)Destroy(mesh);
        }
    }
}
