using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MeshCutting : MonoBehaviour
{
    public bool ScreenCutTest;
    public int Mask;
    [Range(1, 20)]
    public float MaxStep = 5;
    [Range(1, 10)]
    public float MaxDepth = 10;
    public ParentAction _ParentAction = ParentAction.Destroy;
    public bool CombinedSlice = true;
    public bool SliceToSubMesh = false;
    public Material SliceMat;
    public TwoGameObjectEvent EndCuttingCallback;

    new Camera camera;
    Vector3 startScreenPos, endScreenPos;

    private void Awake()
    {
        camera = GetComponent<Camera>();
    }

    private void Update()
    {
        if (ScreenCutTest)
        {
            if (Input.GetMouseButtonDown(0))
            {
                startScreenPos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                endScreenPos = Input.mousePosition;
                ScreenCutting();
            }
        }
    }

    public void Cut(GameObject obj, Vector3 normal, Vector3 point)
    {
        Dictionary<Transform, (Vector3, Vector3)> objs = new Dictionary<Transform, (Vector3, Vector3)>();
        objs.Add(obj.transform, (normal, point));
        Cut(objs);
    }

    public void Cut(Ray ray, Vector3 normal)
    {
        Cut(ray, normal, Mask);
    }

    public void Cut(Ray ray, Vector3 normal, int layerMask)
    {
        Dictionary<Transform, (Vector3, Vector3)> objs = new Dictionary<Transform, (Vector3, Vector3)>();
        RaycastHit[] hits = Physics.RaycastAll(ray, MaxDepth, layerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Transform _trans = hit.transform;
            if (objs.ContainsKey(_trans))
                continue;
            objs.Add(_trans, (normal, hit.point));
        }
        Cut(objs);
    }

    void ScreenCutting()
    {
        Dictionary<Transform, (Vector3, Vector3)> objs = new Dictionary<Transform, (Vector3, Vector3)>();
        float distance = Vector3.Distance(startScreenPos, endScreenPos);
        int step = Mathf.CeilToInt(distance / MaxStep);
        Vector3 dir = camera.ScreenToWorldPoint(new Vector3(endScreenPos.x, endScreenPos.y, MaxDepth)) - camera.ScreenToWorldPoint(new Vector3(startScreenPos.x, startScreenPos.y, MaxDepth));
        while (step >= 0)
        {
            float _dis = step-- * MaxStep;
            _dis = _dis > distance ? distance : _dis;
            Vector3 _pos = startScreenPos + Vector3.Normalize(endScreenPos - startScreenPos) * _dis;
            Ray ray = camera.ScreenPointToRay(_pos);
            RaycastHit[] hits = Physics.RaycastAll(ray, MaxDepth, Mask);
            Vector3 normal = Vector3.Cross(ray.direction, dir).normalized;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Transform _trans = hit.transform;
                if (objs.ContainsKey(_trans))
                    continue;
                objs.Add(_trans, (normal, hit.point));
            }
        }
        Cut(objs);
    }

    void Cut(Dictionary<Transform, (Vector3, Vector3)> sliceTarget)
    {
        foreach (var item in sliceTarget)
        {
            MeshCuttingInfo rootInfo = item.Key.GetComponent<MeshCuttingInfo>();
            List<Transform> trans = new List<Transform>();
            List<Vector3> scaleCover = new List<Vector3>();
            List<Mesh> meshs = new List<Mesh>();
            List<Material[]> mats = new List<Material[]>();
            MeshFilter[] meshFilters = item.Key.GetComponentsInChildren<MeshFilter>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                trans.Add(meshFilter.transform);
                scaleCover.Add(meshFilter.transform.lossyScale);
                meshs.Add(meshFilter.mesh);
                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    mats.Add(new Material[0]);
                else
                    mats.Add(meshRenderer.materials);
            }
            SkinnedMeshRenderer[] skinnedMeshs = item.Key.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < skinnedMeshs.Length; i++)
            {
                SkinnedMeshRenderer skinnedMesh = skinnedMeshs[i];
                Mesh mesh = new Mesh();
                skinnedMesh.BakeMesh(mesh);
                meshs.Add(mesh);
                trans.Add(skinnedMesh.transform);
                scaleCover.Add(Vector3.one);
                mats.Add(skinnedMesh.materials);
            }
            (GameObject, GameObject) rootSliceObj = initRootSliceObj(item.Key);
            for (int i = 0; i < meshs.Count; i++)
            {
                Transform _trans = trans[i];
                Vector3 _scaleCover = scaleCover[i];
                Mesh _mesh = meshs[i];
                Material[] _mat = mats[i];
                MeshCuttingInfo _info = _trans.GetComponent<MeshCuttingInfo>();
                bool hasSelfInfo = true;
                if (_info == null)
                {
                    _info = rootInfo;
                    hasSelfInfo = false;
                }
                Matrix4x4 world2Local = Matrix4x4.TRS(_trans.position, _trans.rotation, _scaleCover).inverse;
                Vector4 pos = item.Value.Item2;
                pos.w = 1;
                Plane plane = new Plane(_trans.InverseTransformDirection(item.Value.Item1), world2Local * pos);
                (GameObject, GameObject) children = cuttingMesh(_mesh, plane, _trans, _scaleCover, _mat, _info);
                if (children.Item1 != null)
                    children.Item1.transform.SetParent(rootSliceObj.Item1.transform);
                if (children.Item2 != null)
                    children.Item2.transform.SetParent(rootSliceObj.Item2.transform);
                if (hasSelfInfo && _info != null && _info != rootInfo)
                    _info.EndCuttingCallback?.Invoke(children.Item1, children.Item2);
            }
            if (rootInfo != null)
                rootInfo.EndCuttingCallback?.Invoke(rootSliceObj.Item1, rootSliceObj.Item2);
            EndCuttingCallback?.Invoke(rootSliceObj.Item1, rootSliceObj.Item2);
            ParentAction action;
            if (rootInfo == null)
                action = _ParentAction;
            else
                action = rootInfo._ParentAction;
            switch (action)
            {
                case ParentAction.Disable:
                    item.Key.gameObject.SetActive(false);
                    break;
                case ParentAction.Destroy:
                    Destroy(item.Key.gameObject);
                    break;
            }
        }
    }

    (GameObject, GameObject) initRootSliceObj(Transform trans)
    {
        GameObject rootA = new GameObject();
        rootA.transform.position = trans.position;
        rootA.transform.rotation = trans.rotation;
        rootA.name = $"{trans.name}-SliceA";

        GameObject rootB = new GameObject();
        rootB.transform.position = trans.position;
        rootB.transform.rotation = trans.rotation;
        rootB.name = $"{trans.name}-SliceB";
        return (rootA, rootB);
    }
    
    (GameObject, GameObject) cuttingMesh(Mesh mesh, Plane plane, Transform trans, Vector3 scaleCover, Material[] mats, MeshCuttingInfo info)
    {
        Vector3 normal = -plane.normal;
        bool combinedSlice = CombinedSlice;
        bool sliceToSubMesh = SliceToSubMesh;
        Material sliceMat = SliceMat;
        if (info != null)
        {
            combinedSlice = info.CombinedSlice;
            sliceToSubMesh = info.SliceToSubMesh;
            sliceMat = info.SliceMat;
        }
        SliceMeshCache a_Cache = new SliceMeshCache();
        SliceMeshCache b_Cache = new SliceMeshCache();
        List<List<Vector3>> extraVertices = new List<List<Vector3>>();
        for (int subIndex = 0; subIndex < mesh.subMeshCount; subIndex++)
        {
            int[] subTriangles = mesh.GetIndices(subIndex);
            SliceMeshCache a_meshCache = new SliceMeshCache();
            SliceMeshCache b_meshCache = new SliceMeshCache();
            a_Cache.SetSubMeshCache(a_meshCache);
            b_Cache.SetSubMeshCache(b_meshCache);
            List<Vector3> extraVertex = new List<Vector3>();
            for (int i = 0; i < subTriangles.Length; i += 3)
            {
                int index0 = subTriangles[i];
                int index1 = subTriangles[i + 1];
                int index2 = subTriangles[i + 2];
                Vector3 vertex0 = mesh.vertices[index0];
                Vector3 vertex1 = mesh.vertices[index1];
                Vector3 vertex2 = mesh.vertices[index2];
                Vector3 normal0 = mesh.normals[index0];
                Vector3 normal1 = mesh.normals[index1];
                Vector3 normal2 = mesh.normals[index2];
                Vector2 uv0 = mesh.uv[index0];
                Vector2 uv1 = mesh.uv[index1];
                Vector2 uv2 = mesh.uv[index2];
                Vector4 tangent0 = mesh.tangents[index0];
                Vector4 tangent1 = mesh.tangents[index1];
                Vector4 tangent2 = mesh.tangents[index2];

                bool side0 = plane.GetSide(vertex0);
                bool side1 = plane.GetSide(vertex1);
                bool side2 = plane.GetSide(vertex2);

                if (side0 && side1 && side2)
                {
                    VertexData vertexData0 = new VertexData() { vertex = vertex0, normal = normal0, uv = uv0, tangent = tangent0 };
                    VertexData vertexData1 = new VertexData() { vertex = vertex1, normal = normal1, uv = uv1, tangent = tangent1 };
                    VertexData vertexData2 = new VertexData() { vertex = vertex2, normal = normal2, uv = uv2, tangent = tangent2 };
                    a_meshCache.SetTriangle(vertexData0, vertexData1, vertexData2);
                }

                if (!side0 && !side1 && !side2)
                {
                    VertexData vertexData0 = new VertexData() { vertex = vertex0, normal = normal0, uv = uv0, tangent = tangent0 };
                    VertexData vertexData1 = new VertexData() { vertex = vertex1, normal = normal1, uv = uv1, tangent = tangent1 };
                    VertexData vertexData2 = new VertexData() { vertex = vertex2, normal = normal2, uv = uv2, tangent = tangent2 };
                    b_meshCache.SetTriangle(vertexData0, vertexData1, vertexData2);
                }

                bool a_res = side0 && side1 && !side2;
                bool b_res = !side0 && !side1 && side2;
                if (a_res || b_res)
                {
                    VertexData forwardVertex = new VertexData() { vertex = vertex2, normal = normal2, uv = uv2, tangent = tangent2 };
                    VertexData backVertex0 = new VertexData() { vertex = vertex0, normal = normal0, uv = uv0, tangent = tangent0 };
                    float backVertex0_disToSlicing = plane.GetDistanceToPoint(vertex0);
                    VertexData backVertex1 = new VertexData() { vertex = vertex1, normal = normal1, uv = uv1, tangent = tangent1 };
                    float backVertex1_disToSlicing = plane.GetDistanceToPoint(vertex1);
                    (VertexData, VertexData) cutVertexs = slicingTriangle(normal, forwardVertex, backVertex0, backVertex0_disToSlicing, backVertex1, backVertex1_disToSlicing);
                    if (a_res)
                    {
                        a_meshCache.SetTriangle(backVertex0, backVertex1, cutVertexs.Item2);
                        a_meshCache.SetTriangle(backVertex0, cutVertexs.Item2, cutVertexs.Item1);
                        b_meshCache.SetTriangle(cutVertexs.Item1, cutVertexs.Item2, forwardVertex);
                    }
                    if (b_res)
                    {
                        b_meshCache.SetTriangle(backVertex0, backVertex1, cutVertexs.Item2);
                        b_meshCache.SetTriangle(backVertex0, cutVertexs.Item2, cutVertexs.Item1);
                        a_meshCache.SetTriangle(cutVertexs.Item1, cutVertexs.Item2, forwardVertex);
                    }
                    extraVertex.Add(cutVertexs.Item1.vertex);
                    extraVertex.Add(cutVertexs.Item2.vertex);
                }

                a_res = side0 && side2 && !side1;
                b_res = !side0 && !side2 && side1;
                if (a_res || b_res)
                {
                    VertexData forwardVertex = new VertexData() { vertex = vertex1, normal = normal1, uv = uv1, tangent = tangent1 };
                    VertexData backVertex0 = new VertexData() { vertex = vertex0, normal = normal0, uv = uv0, tangent = tangent0 };
                    float backVertex0_disToSlicing = plane.GetDistanceToPoint(vertex0);
                    VertexData backVertex1 = new VertexData() { vertex = vertex2, normal = normal2, uv = uv2, tangent = tangent2 };
                    float backVertex1_disToSlicing = plane.GetDistanceToPoint(vertex2);
                    (VertexData, VertexData) cutVertexs = slicingTriangle(normal, forwardVertex, backVertex0, backVertex0_disToSlicing, backVertex1, backVertex1_disToSlicing);
                    if (a_res)
                    {
                        a_meshCache.SetTriangle(backVertex0, cutVertexs.Item1, cutVertexs.Item2);
                        a_meshCache.SetTriangle(backVertex0, cutVertexs.Item2, backVertex1);
                        b_meshCache.SetTriangle(cutVertexs.Item1, forwardVertex, cutVertexs.Item2);
                    }
                    if (b_res)
                    {
                        b_meshCache.SetTriangle(backVertex0, cutVertexs.Item1, cutVertexs.Item2);
                        b_meshCache.SetTriangle(backVertex0, cutVertexs.Item2, backVertex1);
                        a_meshCache.SetTriangle(cutVertexs.Item1, forwardVertex, cutVertexs.Item2);
                    }
                    extraVertex.Add(cutVertexs.Item1.vertex);
                    extraVertex.Add(cutVertexs.Item2.vertex);
                }

                a_res = side1 && side2 && !side0;
                b_res = !side1 && !side2 && side0;
                if (a_res || b_res)
                {
                    VertexData forwardVertex = new VertexData() { vertex = vertex0, normal = normal0, uv = uv0, tangent = tangent0 };
                    VertexData backVertex0 = new VertexData() { vertex = vertex1, normal = normal1, uv = uv1, tangent = tangent1 };
                    float backVertex0_disToSlicing = plane.GetDistanceToPoint(vertex1);
                    VertexData backVertex1 = new VertexData() { vertex = vertex2, normal = normal2, uv = uv2, tangent = tangent2 };
                    float backVertex1_disToSlicing = plane.GetDistanceToPoint(vertex2);
                    (VertexData, VertexData) cutVertexs = slicingTriangle(normal, forwardVertex, backVertex0, backVertex0_disToSlicing, backVertex1, backVertex1_disToSlicing);

                    if (a_res)
                    {
                        a_meshCache.SetTriangle(backVertex0, backVertex1, cutVertexs.Item2);
                        a_meshCache.SetTriangle(backVertex0, cutVertexs.Item2, cutVertexs.Item1);
                        b_meshCache.SetTriangle(forwardVertex, cutVertexs.Item1, cutVertexs.Item2);
                    }
                    if (b_res)
                    {
                        b_meshCache.SetTriangle(backVertex0, backVertex1, cutVertexs.Item2);
                        b_meshCache.SetTriangle(backVertex0, cutVertexs.Item2, cutVertexs.Item1);
                        a_meshCache.SetTriangle(forwardVertex, cutVertexs.Item1, cutVertexs.Item2);
                    }
                    extraVertex.Add(cutVertexs.Item1.vertex);
                    extraVertex.Add(cutVertexs.Item2.vertex);
                }
            }
            if (combinedSlice)
            {
                if (sliceToSubMesh)
                    extraVertices.Add(extraVertex);
                else
                {
                    List<Vector2> uvs = CalcUV(normal, extraVertex);
                    CombineTriangle(extraVertex, uvs, normal, a_meshCache, b_meshCache);
                }
            }
        }
        if (sliceToSubMesh)
        {
            for (int i = 0; i < extraVertices.Count; i++)
            {
                List<Vector3> extraVertex = extraVertices[i];
                List<Vector2> uvs = CalcUV(normal, extraVertex);
                SliceMeshCache a_sliceMeshCache = new SliceMeshCache();
                SliceMeshCache b_sliceMeshCache = new SliceMeshCache();
                CombineTriangle(extraVertex, uvs, normal, a_sliceMeshCache, b_sliceMeshCache);
                a_Cache.SetSubMeshCache(a_sliceMeshCache);
                b_Cache.SetSubMeshCache(b_sliceMeshCache);
            }
        }

        Mesh a_mesh = a_Cache.ToMesh();
        Mesh b_mesh = b_Cache.ToMesh();
        Material[] _mat;
        if (a_mesh != null && b_mesh != null)
        {
            if (sliceToSubMesh)
            {
                List<Material> matList = new List<Material>();
                matList.AddRange(mats);
                matList.Add(sliceMat);
                _mat = matList.ToArray();
            }
            else
                _mat = mats;
        }
        else
            _mat = mats;
        GameObject a_obj = null;
        if (a_mesh != null)
        {
            a_obj = new GameObject();
            a_obj.name = "A";
            a_obj.AddComponent<MeshFilter>().mesh = a_mesh;
            a_obj.AddComponent<MeshRenderer>().materials = _mat;
            a_obj.transform.position = trans.position;
            a_obj.transform.eulerAngles = trans.eulerAngles;
            a_obj.transform.localScale = scaleCover;
        }
        GameObject b_obj = null;
        if (b_mesh != null)
        {
            b_obj = new GameObject();
            b_obj.name = "B";
            b_obj.AddComponent<MeshFilter>().mesh = b_mesh;
            b_obj.AddComponent<MeshRenderer>().materials = _mat;
            b_obj.transform.position = trans.position;
            b_obj.transform.eulerAngles = trans.eulerAngles;
            b_obj.transform.localScale = scaleCover;
        }
        return (a_obj, b_obj);
    }

    void CombineTriangle(List<Vector3> vertices, List<Vector2> uvs, Vector3 normal, SliceMeshCache a_meshCache, SliceMeshCache b_meshCache)
    {
        if (vertices.Count < 3)
            return;
        Vector3 vertex0 = vertices[0];
        Vector3 vertex1 = vertices[1];
        Vector3 uv0 = uvs[0];
        Vector3 uv1 = uvs[1];
        vertices.RemoveAt(0);
        vertices.RemoveAt(0);
        uvs.RemoveAt(0);
        uvs.RemoveAt(0);
        while (vertices.Count > 0)
        {
            VertexData a_vertex0 = new VertexData() { vertex = vertex0, normal = normal, uv = uv0 };
            VertexData b_vertex0 = new VertexData() { vertex = vertex0, normal = -normal, uv = uv0 };
            VertexData a_vertex1 = new VertexData() { vertex = vertex1, normal = normal, uv = uv1 };
            VertexData b_vertex1 = new VertexData() { vertex = vertex1, normal = -normal, uv = uv1 };
            Vector3 vertex2 = vertices[0];
            Vector3 uv2 = uvs[0];
            vertices.RemoveAt(0);
            uvs.RemoveAt(0);
            VertexData a_vertex2 = new VertexData() { vertex = vertex2, normal = normal, uv = uv2 };
            VertexData b_vertex2 = new VertexData() { vertex = vertex2, normal = -normal, uv = uv2 };

            Vector3 dir = Vector3.Normalize(vertex1 - vertex0);
            Vector3 _dir = Vector3.Normalize(vertex2 - vertex0);
            if (Vector3.Dot(normal, Vector3.Cross(dir, _dir)) >= 0)
            {
                CalcTangent(ref a_vertex0, ref a_vertex1, ref a_vertex2);
                a_meshCache.SetTriangle(a_vertex0, a_vertex1, a_vertex2);
                CalcTangent(ref b_vertex0, ref b_vertex2, ref b_vertex1);
                b_meshCache.SetTriangle(b_vertex0, b_vertex2, b_vertex1);
            }
            else
            {
                CalcTangent(ref a_vertex0, ref a_vertex2, ref a_vertex1);
                a_meshCache.SetTriangle(a_vertex0, a_vertex2, a_vertex1);
                CalcTangent(ref b_vertex0, ref b_vertex1, ref b_vertex2);
                b_meshCache.SetTriangle(b_vertex0, b_vertex1, b_vertex2);
            }
            vertex1 = vertex2;
            uv1 = uv2;
        }
    }

    void CombineTriangle(List<Vector3> vertices, List<Vector2> uvs, Vector3 normal, SliceMeshCache a_meshCache, SliceMeshCache b_meshCache, bool root)
    {
        if (vertices.Count < 2)
            return;
        Vector3 vertex0 = vertices[0];
        Vector3 vertex1 = vertices[1];
        Vector2 uv0 = uvs[0];
        Vector2 uv1 = uvs[1];
        vertices.RemoveAt(0);
        vertices.RemoveAt(0);
        uvs.RemoveAt(0);
        uvs.RemoveAt(0);
        while (vertices.Count > 0)
        {
            VertexData a_vertex0 = new VertexData() { vertex = vertex0, normal = normal, uv = uv0 };
            VertexData a_vertex1 = new VertexData() { vertex = vertex1, normal = normal, uv = uv1 };
            VertexData b_vertex0 = new VertexData() { vertex = vertex0, normal = -normal, uv = uv0 };
            VertexData b_vertex1 = new VertexData() { vertex = vertex1, normal = -normal, uv = uv1 };
            Vector3 dir = Vector3.Normalize(vertex1 - vertex0);
            if (vertices.Count > 1)
            {
                int sameIndex = vertices.IndexOf(vertex1);
                if (sameIndex > -1)
                {
                    Vector3 vertex2;
                    Vector2 uv2;
                    if (sameIndex % 2 == 0)
                    {
                        vertex2 = vertices[sameIndex + 1];
                        uv2 = uvs[sameIndex + 1];
                        vertices.RemoveAt(sameIndex);
                        vertices.RemoveAt(sameIndex);
                        uvs.RemoveAt(sameIndex);
                        uvs.RemoveAt(sameIndex);
                    }
                    else
                    {
                        vertex2 = vertices[sameIndex - 1];
                        uv2 = uvs[sameIndex - 1];
                        vertices.RemoveAt(sameIndex);
                        vertices.RemoveAt(sameIndex - 1);
                        uvs.RemoveAt(sameIndex);
                        uvs.RemoveAt(sameIndex - 1);
                    }
                    VertexData a_vertex2 = new VertexData() { vertex = vertex2, normal = normal, uv = uv2 };
                    VertexData b_vertex2 = new VertexData() { vertex = vertex2, normal = -normal, uv = uv2 };
                    Vector3 _dir = Vector3.Normalize(vertex2 - vertex0);
                    if (Vector3.Dot(normal, Vector3.Cross(dir, _dir)) >= 0)
                    {
                        CalcTangent(ref a_vertex0, ref a_vertex1, ref a_vertex2);
                        a_meshCache.SetTriangle(a_vertex0, a_vertex1, a_vertex2);
                        CalcTangent(ref b_vertex0, ref b_vertex2, ref b_vertex1);
                        b_meshCache.SetTriangle(b_vertex0, b_vertex2, b_vertex1);
                    }
                    else
                    {
                        CalcTangent(ref a_vertex0, ref a_vertex2, ref a_vertex1);
                        a_meshCache.SetTriangle(a_vertex0, a_vertex2, a_vertex1);
                        CalcTangent(ref b_vertex0, ref b_vertex1, ref b_vertex2);
                        b_meshCache.SetTriangle(b_vertex0, b_vertex1, b_vertex2);
                    }
                    if (vertices.IndexOf(vertex1) > -1)
                    {
                        vertices.Insert(0, vertex1);
                        vertices.Insert(0, vertex0);
                        uvs.Insert(0, uv1);
                        uvs.Insert(0, uv0);
                        CombineTriangle(vertices, uvs, normal, a_meshCache, b_meshCache, false);
                    }
                    vertex1 = vertex2;
                    uv1 = uv2;
                }
                else if (root)
                {
                    if (vertices.IndexOf(vertex0) > -1)
                    {
                        vertices.Insert(0, vertex0);
                        vertices.Insert(0, vertex1);
                        uvs.Insert(0, uv0);
                        uvs.Insert(0, uv1);
                        CombineTriangle(vertices, uvs, normal, a_meshCache, b_meshCache, true);
                    }
                    else
                        CombineTriangle(vertices, uvs, normal, a_meshCache, b_meshCache, true);
                }
                else
                    break;
            }
            else
            {
                Vector3 vertex2 = vertices[0];
                Vector2 uv2 = uvs[0];
                vertices.RemoveAt(0);
                uvs.RemoveAt(0);
                VertexData a_vertex2 = new VertexData() { vertex = vertex2, normal = normal, uv = uv2 };
                VertexData b_vertex2 = new VertexData() { vertex = vertex2, normal = -normal, uv = uv2 };
                Vector3 _dir = vertex2 - vertex0;
                if (Vector3.Dot(normal, Vector3.Cross(dir, _dir)) >= 0)
                {
                    CalcTangent(ref a_vertex0, ref a_vertex1, ref a_vertex2);
                    a_meshCache.SetTriangle(a_vertex0, a_vertex1, a_vertex2);
                    CalcTangent(ref b_vertex0, ref b_vertex2, ref b_vertex1);
                    b_meshCache.SetTriangle(b_vertex0, b_vertex2, b_vertex1);
                }
                else
                {
                    CalcTangent(ref a_vertex0, ref a_vertex2, ref a_vertex1);
                    a_meshCache.SetTriangle(a_vertex0, a_vertex2, a_vertex1);
                    CalcTangent(ref b_vertex0, ref b_vertex1, ref b_vertex2);
                    b_meshCache.SetTriangle(b_vertex0, b_vertex1, b_vertex2);
                }
            }
        }
    }

    void CalcTangent(ref VertexData vertex0, ref VertexData vertex1, ref VertexData vertex2)
    {
        float x1 = vertex1.vertex.x - vertex0.vertex.x;
        float x2 = vertex2.vertex.x - vertex0.vertex.x;
        float y1 = vertex1.vertex.y - vertex0.vertex.y;
        float y2 = vertex2.vertex.y - vertex0.vertex.y;
        float z1 = vertex1.vertex.z - vertex0.vertex.z;
        float z2 = vertex2.vertex.z - vertex0.vertex.z;

        float s1 = vertex1.uv.x - vertex0.uv.x;
        float s2 = vertex2.uv.x - vertex0.uv.x;
        float t1 = vertex1.uv.y - vertex0.uv.y;
        float t2 = vertex2.uv.y - vertex0.uv.y;

        float r = 1f / (s1 * t2 - s2 * t1);
        Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
        Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

        Vector3 tan1_1 = sdir;
        Vector3 tan1_2 = sdir;
        Vector3 tan1_3 = sdir;

        Vector3 tan2_1 = tdir;
        Vector3 tan2_2 = tdir;
        Vector3 tan2_3 = tdir;

        Vector3 normal1 = vertex0.normal;
        Vector3.OrthoNormalize(ref normal1, ref tan1_1);
        vertex0.tangent.x = tan1_1.x;
        vertex0.tangent.y = tan1_1.y;
        vertex0.tangent.z = tan1_1.z;
        int tW1 = (Vector3.Dot(Vector3.Cross(normal1, tan1_1), tan2_1) < 0) ? -1 : 1;
        vertex0.tangent.w = tW1;

        Vector3 normal2 = vertex1.normal;
        Vector3.OrthoNormalize(ref normal2, ref tan1_2);
        vertex1.tangent.x = tan1_2.x;
        vertex1.tangent.y = tan1_2.y;
        vertex1.tangent.z = tan1_2.z;
        int tW2 = (Vector3.Dot(Vector3.Cross(normal2, tan1_2), tan2_2) < 0) ? -1 : 1;
        vertex1.tangent.w = tW2;

        Vector3 normal3 = vertex2.normal;
        Vector3.OrthoNormalize(ref normal3, ref tan1_3);
        vertex2.tangent.x = tan1_3.x;
        vertex2.tangent.y = tan1_3.y;
        vertex2.tangent.z = tan1_3.z;
        int tW3 = (Vector3.Dot(Vector3.Cross(normal3, tan1_3), tan2_3) < 0) ? -1 : 1;
        vertex2.tangent.w = tW3;
    }

    List<Vector2> CalcUV(Vector3 commonNormal, List<Vector3> vertices)
    {
        Rect uvRect = new Rect();
        List<Vector2> uvs = new List<Vector2>();
        float dot_x = Mathf.Abs(Vector3.Dot(Vector3.right, commonNormal));
        float dot_y = Mathf.Abs(Vector3.Dot(Vector3.up, commonNormal));
        float dot_z = Mathf.Abs(Vector3.Dot(Vector3.forward, commonNormal));
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 vertex = vertices[i];
            Vector2 uv;
            if (dot_x > dot_y)
            {
                if (dot_x > dot_z)
                    uv = new Vector2(vertex.y, vertex.z);
                else
                    uv = new Vector2(vertex.y, vertex.x);
            }
            else
            {
                if (dot_y > dot_z)
                    uv = new Vector2(vertex.x, vertex.z);
                else
                    uv = new Vector2(vertex.x, vertex.y);
            }
            uvs.Add(uv);
            uvRect.xMin = uvRect.xMin > uv.x ? uv.x : uvRect.xMin;
            uvRect.xMax = uvRect.xMax < uv.x ? uv.x : uvRect.xMax;
            uvRect.yMin = uvRect.yMin > uv.y ? uv.y : uvRect.yMin;
            uvRect.yMax = uvRect.yMax < uv.y ? uv.y : uvRect.yMax;
        }
        for (int i = 0; i < uvs.Count; i++)
        {
            Vector2 uv = uvs[i];
            uv.x = remap(uv.x, uvRect.xMin, uvRect.xMax, 0, 1);
            uv.y = remap(uv.y, uvRect.yMin, uvRect.yMax, 0, 1);
            uvs[i] = uv;
        }
        return uvs;
    }

    float remap(float num, float inMin, float inMax, float outMin, float outMax)
    {
        return outMin + (num - inMin) * (outMax - outMin) / (inMax - inMin);
    }

    (VertexData, VertexData) slicingTriangle(Vector3 slicingNormal, VertexData forwardVertex, VertexData backVertex0, float backVertex0_disToSlicing, VertexData backVertex1, float backVertex1_disToSlicing)
    {
        VertexData slicingVertex0 = new VertexData();
        Vector3 verDir0 = Vector3.Normalize(forwardVertex.vertex - backVertex0.vertex);
        float inv_dot0 = 1 / Vector3.Dot(slicingNormal, verDir0);
        Vector3 slicingPos0 = backVertex0.vertex + verDir0 * backVertex0_disToSlicing * inv_dot0;
        float slicingRefer0 = Vector3.Distance(slicingPos0, forwardVertex.vertex) / Vector3.Distance(backVertex0.vertex, forwardVertex.vertex);
        Vector3 normal0 = Vector3.Lerp(forwardVertex.normal, backVertex0.normal, slicingRefer0);
        Vector2 uv0 = Vector2.Lerp(forwardVertex.uv, backVertex0.uv, slicingRefer0);
        Vector4 tangent0 = Vector4.Lerp(forwardVertex.tangent, backVertex0.tangent, slicingRefer0);
        tangent0.w = Mathf.Sign(tangent0.w);
        //slicingPos0.x = (float)Math.Round(slicingPos0.x, 3);
        //slicingPos0.y = (float)Math.Round(slicingPos0.y, 3);
        //slicingPos0.z = (float)Math.Round(slicingPos0.z, 3);
        slicingVertex0.vertex = slicingPos0;
        slicingVertex0.normal = normal0;
        slicingVertex0.uv = uv0;
        slicingVertex0.tangent = tangent0;
;
        VertexData slicingVertex1 = new VertexData();
        Vector3 verDir1 = Vector3.Normalize(forwardVertex.vertex - backVertex1.vertex);
        float inv_dot1 = 1 / Vector3.Dot(slicingNormal, verDir1);
        Vector3 slicingPos1 = backVertex1.vertex + verDir1 * backVertex1_disToSlicing * inv_dot1;
        float slicingRefer1 = Vector3.Distance(slicingPos1, forwardVertex.vertex) / Vector3.Distance(backVertex1.vertex, forwardVertex.vertex);
        Vector3 normal1 = Vector3.Lerp(forwardVertex.normal, backVertex1.normal, slicingRefer1);
        Vector2 uv1 = Vector2.Lerp(forwardVertex.uv, backVertex1.uv, slicingRefer1);
        Vector4 tangent1 = Vector4.Lerp(forwardVertex.tangent, backVertex1.tangent, slicingRefer1);
        tangent1.w = Mathf.Sign(tangent1.w);
        //slicingPos1.x = (float)Math.Round(slicingPos1.x, 3);
        //slicingPos1.y = (float)Math.Round(slicingPos1.y, 3);
        //slicingPos1.z = (float)Math.Round(slicingPos1.z, 3);
        slicingVertex1.vertex = slicingPos1;
        slicingVertex1.normal = normal1;
        slicingVertex1.uv = uv1;
        slicingVertex1.tangent = tangent1;
        return (slicingVertex0, slicingVertex1);
    }

    class SliceMeshCache
    {
        List<Vector3> vertices;
        List<Vector3> normals;
        List<Vector2> uvs;
        List<Vector4> tangents;
        List<int> triangles;
        List<SliceMeshCache> subMeshCaches;

        public SliceMeshCache()
        {
            vertices = new List<Vector3>();
            normals = new List<Vector3>();
            uvs = new List<Vector2>();
            tangents = new List<Vector4>();
            triangles = new List<int>();
            subMeshCaches = new List<SliceMeshCache>();
        }

        public void SetTriangle(VertexData vertex0, VertexData vertex1, VertexData vertex2)
        {
            int _index0 = vertices.Count;
            vertices.Add(vertex0.vertex);
            int _index1 = vertices.Count;
            vertices.Add(vertex1.vertex);
            int _index2 = vertices.Count;
            vertices.Add(vertex2.vertex);
            normals.Add(vertex0.normal);
            normals.Add(vertex1.normal);
            normals.Add(vertex2.normal);
            uvs.Add(vertex0.uv);
            uvs.Add(vertex1.uv);
            uvs.Add(vertex2.uv);
            tangents.Add(vertex0.tangent);
            tangents.Add(vertex1.tangent);
            tangents.Add(vertex2.tangent);
            triangles.Add(_index0);
            triangles.Add(_index1);
            triangles.Add(_index2);
        }

        public void SetSubMeshCache(params SliceMeshCache[] subMeshCache)
        {
            subMeshCaches.AddRange(subMeshCache);
        }

        public Mesh ToMesh()
        {
            List<CombineInstance> combineInstances = new List<CombineInstance>();
            if (vertices.Count > 0)
            {
                Mesh mesh = new Mesh();
                mesh.vertices = vertices.ToArray();
                mesh.normals = normals.ToArray();
                mesh.uv = uvs.ToArray();
                mesh.tangents = tangents.ToArray();
                mesh.triangles = triangles.ToArray();
                combineInstances.Add(new CombineInstance() { mesh = mesh, transform = Matrix4x4.identity, subMeshIndex = 0 });
            }
            for (int i = 0; i < subMeshCaches.Count; i++)
            {
                SliceMeshCache meshCache = subMeshCaches[i];
                if (meshCache == null)
                    continue;
                Mesh subMesh = meshCache.ToMesh();
                if (subMesh == null)
                    continue;
                for (int subIndex = 0; subIndex < subMesh.subMeshCount; subIndex++)
                    combineInstances.Add(new CombineInstance() { mesh = subMesh, transform = Matrix4x4.identity, subMeshIndex = subIndex });
            }
            if (combineInstances.Count > 0)
            {
                Mesh finalMesh = new Mesh();
                finalMesh.CombineMeshes(combineInstances.ToArray(), false);
                return finalMesh;
            }
            return null;
        }
    }

    struct VertexData
    {
        public Vector3 vertex;
        public Vector3 normal;
        public Vector2 uv;
        public Vector4 tangent;
    }

    public enum ParentAction { None, Disable, Destroy }
}