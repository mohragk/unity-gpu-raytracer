using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{
    struct Sphere
    {
        public Vector3 position;
        public float radius;

        public Vector3 albedo;
        public Vector3 specular;
    };

    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;

    public Light directionalLight;

    [Header("Spheres")]
    public int sphereSeed;
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;
    private ComputeBuffer _sphereBuffer;

    private RenderTexture _target;
    private RenderTexture _converged;
    private Material _addMaterial;

    private uint _currentSample;

    private List<Transform> _transformsToWatch = new List<Transform>();




    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        _transformsToWatch.Add(directionalLight.transform);
        _transformsToWatch.Add(transform);
    }


    private void OnEnable() {
        _currentSample = 0;
        setupScene();
    }

    private void setupScene() {
        Random.InitState(sphereSeed);
        List<Sphere> spheres = new List<Sphere>();
        // Add a number of random spheres
        for (int i = 0; i < SpheresMax; i++)
            {
                Sphere sphere = new Sphere();
                // Radius and radius
                sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
                Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
                sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
                // Reject spheres that are intersecting others
                foreach (Sphere other in spheres)
                {
                    float minDist = sphere.radius + other.radius;
                    if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                        goto SkipSphere;
                }
                // Albedo and specular color
                Color color = Random.ColorHSV();
                bool metal = Random.value < 0.5f;
                sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
                // Add the sphere to the list
                spheres.Add(sphere);
            
                SkipSphere:
                continue;
            }
        // Assign to compute buffer
        // Assign to compute buffer
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        
        if (spheres.Count > 0) {
            _sphereBuffer = new ComputeBuffer(spheres.Count, 40);
            _sphereBuffer.SetData(spheres);
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        SetShaderParameters();
        Render(destination);
    }

    void Update()
    {
        foreach (Transform t in _transformsToWatch)
        {
            if (t.hasChanged)
            {
                _currentSample = 0;
                t.hasChanged = false;
            }
        }
    }


    private void Render(RenderTexture destination) {
        InitRenderTexture();

        RayTracingShader.SetTexture(0, "Result", _target);
        
        int threadgroups_x = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadgroups_y = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadgroups_x, threadgroups_y, 1);

        if (_addMaterial == null) {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }

        _addMaterial.SetFloat("_Sample", _currentSample);


        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);
        //Graphics.Blit(_target, destination, _addMaterial);
        _currentSample++;
    }

    private void InitRenderTexture() {
        if (
            _target == null ||
            _target.width  != Screen.width ||
            _target.height != Screen.height
            )
        {
            if (_target != null) {
                _target.Release();
                _converged.Release();
            }

            _target = new RenderTexture(
                    Screen.width, 
                    Screen.height, 
                    0,
                    RenderTextureFormat.ARGBFloat, 
                    RenderTextureReadWrite.Linear
                );
            _target.enableRandomWrite = true;
            _target.Create();

            _converged = new RenderTexture(
                   Screen.width,
                   Screen.height,
                   0,
                   RenderTextureFormat.ARGBFloat,
                   RenderTextureReadWrite.Linear
               );
            _converged.enableRandomWrite = true;
            _converged.Create();

            _currentSample = 0;
        }

      
    }

    

    private void SetShaderParameters() {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        RayTracingShader.SetFloat("_Seed", Random.value);

        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_pixelOffset", new Vector2(Random.value, Random.value));

        Vector3 l = directionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, directionalLight.intensity));

        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    

}
