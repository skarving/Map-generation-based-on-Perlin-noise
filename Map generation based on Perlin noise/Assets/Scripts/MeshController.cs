﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshController
{
    public static MeshDetails BuildMesh(float[,] noiseArea, int lod, AreaDetails areaDetails)
    {
        int lodIncrement = lod == 0 ? 1 : lod * 2;
        int verticesPerLine = areaDetails.verticesPerLine;

        Vector2 topLeftCorner = new Vector2(-1, 1) * areaDetails.resolution / 2f;

        MeshDetails meshDetails = new MeshDetails(verticesPerLine, areaDetails.useFlatshading, lodIncrement);

        int[,] vertexIndexes = new int[verticesPerLine, verticesPerLine];
        int meshVertexIndex = 0;
        int outsideVertexIndex = -1;

        for(int yIndex = 0; yIndex < verticesPerLine; yIndex++)
        {
            for(int xIndex = 0; xIndex < verticesPerLine; xIndex++)
            {
                bool isOutsideVertex = yIndex == 0 || xIndex == 0 || yIndex == verticesPerLine - 1 || xIndex == verticesPerLine - 1;
                bool isUselessVertex = xIndex > 2 && xIndex < verticesPerLine - 3 && yIndex > 2 && yIndex < verticesPerLine - 3 && ((xIndex - 2) % lodIncrement != 0 || (yIndex - 2) % lodIncrement != 0);

                if(isOutsideVertex)
                {
                    vertexIndexes[xIndex, yIndex] = outsideVertexIndex;
                    outsideVertexIndex--;
                }
                else if(!isUselessVertex)
                {
                    vertexIndexes[xIndex, yIndex] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for(int yIndex = 0; yIndex < verticesPerLine; yIndex++)
        {
            for(int xIndex = 0; xIndex < verticesPerLine; xIndex++)
            {
                bool isUselessVertex = xIndex > 2 && xIndex < verticesPerLine - 3 && yIndex > 2 && yIndex < verticesPerLine - 3 && ((xIndex - 2) % lodIncrement != 0 || (yIndex - 2) % lodIncrement != 0);

                if(!isUselessVertex)
                {
                    bool isOutsideVertex = yIndex == 0 || xIndex == 0 || yIndex == verticesPerLine - 1 || xIndex == verticesPerLine - 1;
                    bool isFrontierVertex = (yIndex == 1 || yIndex == verticesPerLine - 2 || xIndex == 1 || xIndex == verticesPerLine - 2) && !isOutsideVertex;
                    bool isMainVertex = (xIndex - 2) % lodIncrement == 0 && (yIndex - 2) % lodIncrement == 0 && !isOutsideVertex && !isFrontierVertex;
                    bool isInsideVertex = (yIndex == 2 || yIndex == verticesPerLine - 3 || xIndex == 2 || xIndex == verticesPerLine - 3) && !isOutsideVertex && !isFrontierVertex && !isMainVertex;

                    int vertexIndex = vertexIndexes[xIndex, yIndex];

                    Vector2 uv = new Vector2(xIndex - 1, yIndex - 1) / (verticesPerLine - 3);
                    Vector2 vertexCoords = topLeftCorner + new Vector2(uv.x, -uv.y) * areaDetails.resolution;
                    float height = noiseArea[xIndex, yIndex];

                    if (isInsideVertex)
                    {
                        bool isVertical = xIndex == 2 || xIndex == verticesPerLine - 3;

                        int distanceToMainVertexA = (isVertical ? yIndex - 2 : xIndex - 2) % lodIncrement;
                        int distanceToMainVertexB = lodIncrement - distanceToMainVertexA;

                        float distancePercentage = distanceToMainVertexA / (float)lodIncrement;

                        float heightVertexA = noiseArea[isVertical ? xIndex : xIndex - distanceToMainVertexA, isVertical ? yIndex - distanceToMainVertexA : yIndex];
                        float heightVertexB = noiseArea[isVertical ? xIndex : xIndex + distanceToMainVertexB, isVertical ? yIndex + distanceToMainVertexB : yIndex];

                        height = heightVertexA * (1 - distancePercentage) + heightVertexB * distancePercentage;
                    }

                    meshDetails.AddVertex(new Vector3(vertexCoords.x, height, vertexCoords.y), uv, vertexIndex);

                    bool createTriangle = xIndex < verticesPerLine - 1 && yIndex < verticesPerLine - 1 && (!isInsideVertex || (xIndex != 2 && yIndex != 2));

                    if (createTriangle)
                    {
                        int currentIncrement = (isMainVertex && xIndex != verticesPerLine - 3 && yIndex != verticesPerLine - 3) ? lodIncrement : 1;

                        int vertexAIndex = vertexIndexes[xIndex, yIndex];
                        int vertexBIndex = vertexIndexes[xIndex + currentIncrement, yIndex];
                        int vertexCIndex = vertexIndexes[xIndex, yIndex + currentIncrement];
                        int vertexDIndex = vertexIndexes[xIndex + currentIncrement, yIndex + currentIncrement];

                        meshDetails.AddTriangle(vertexAIndex, vertexDIndex, vertexCIndex);
                        meshDetails.AddTriangle(vertexDIndex, vertexAIndex, vertexBIndex);
                    }
                }
            }
        }

        meshDetails.BuildLighting();

        return meshDetails;
    }
}

public class MeshDetails
{
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;
    private Vector3[] outsideVertices;
    private int[] outsideTriangles;
    private Vector3[] normals;

    private int currentTriangleIndex;
    private int currentOutsideTriangleIndex;
    private bool useFlatshading;

    public MeshDetails(int verticesPerLine, bool useFlatshading, int lodIncrement)
    {
        int frontierVertices = (verticesPerLine - 2) * 4 - 4;
        int insideVertices = (lodIncrement - 1) * (verticesPerLine - 5) / lodIncrement * 4;
        int mainVericesPerLine = (verticesPerLine - 5) / lodIncrement + 1;
        int mainVertices = mainVericesPerLine * mainVericesPerLine;

        vertices = new Vector3[frontierVertices + insideVertices + mainVertices];
        uvs = new Vector2[vertices.Length];

        int meshEdgeTriangles = 8 * (verticesPerLine - 4);
        int mainTriangles = (mainVericesPerLine - 1) * (mainVericesPerLine - 1) * 2;
        triangles = new int[(meshEdgeTriangles + mainTriangles) * 3];

        outsideVertices = new Vector3[verticesPerLine * 4 - 4];
        outsideTriangles = new int[24 * (verticesPerLine - 2)];

        this.useFlatshading = useFlatshading;
    }

    public void AddTriangle(int vertexAIndex, int vertexBIndex, int vertexCIndex)
    {
        if(vertexAIndex < 0 || vertexBIndex < 0 || vertexCIndex < 0)
        {
            outsideTriangles[currentOutsideTriangleIndex] = vertexAIndex;
            outsideTriangles[currentOutsideTriangleIndex + 1] = vertexBIndex;
            outsideTriangles[currentOutsideTriangleIndex + 2] = vertexCIndex;

            currentOutsideTriangleIndex += 3;
        }
        else
        {
            triangles[currentTriangleIndex] = vertexAIndex;
            triangles[currentTriangleIndex + 1] = vertexBIndex;
            triangles[currentTriangleIndex + 2] = vertexCIndex;

            currentTriangleIndex += 3;
        }
    }

    public void AddVertex(Vector3 vertexCoords, Vector2 uv, int vertexIndex)
    {
        if(vertexIndex < 0)
        {
            outsideVertices[-vertexIndex - 1] = vertexCoords;
        }
        else
        {
            vertices[vertexIndex] = vertexCoords;
            uvs[vertexIndex] = uv;
        }
    }

    public void BuildLighting()
    {
        if(useFlatshading)
        {
            Flatshading();
        }
        else
        {
            BuildNormals();
        }
    }

    private void BuildNormals()
    {
        normals = RecomputeNormals();
    }

    public Mesh BuildMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        if(useFlatshading)
        {
            mesh.RecalculateNormals();
        }
        else
        {
            mesh.normals = normals;
        }

        return mesh;
    }

    private void Flatshading()
    {
        Vector3[] vertices = new Vector3[triangles.Length];
        Vector2[] uvs = new Vector2[triangles.Length];
        
        for(int index = 0; index < triangles.Length; index++)
        {
            vertices[index] = this.vertices[triangles[index]];
            uvs[index] = this.uvs[triangles[index]];
            triangles[index] = index;
        }

        this.vertices = vertices;
        this.uvs = uvs;
    }

    private Vector3[] RecomputeNormals()
    {
        Vector3[] normals = new Vector3[vertices.Length];
        int trianglesAmount = triangles.Length / 3;

        for(int index = 0; index < trianglesAmount; index++)
        {
            int triangleIndex = index * 3;

            int vertexAIndex = triangles[triangleIndex];
            int vertexBIndex = triangles[triangleIndex + 1];
            int vertexCIndex = triangles[triangleIndex + 2];

            Vector3 normal = TriangleNormal(vertexAIndex, vertexBIndex, vertexCIndex);

            normals[vertexAIndex] += normal;
            normals[vertexBIndex] += normal;
            normals[vertexCIndex] += normal;
        }

        int frontierTrianglesAmount = outsideTriangles.Length / 3;

        for(int index = 0; index < frontierTrianglesAmount; index++)
        {
            int triangleIndex = index * 3;

            int vertexAIndex = outsideTriangles[triangleIndex];
            int vertexBIndex = outsideTriangles[triangleIndex + 1];
            int vertexCIndex = outsideTriangles[triangleIndex + 2];

            Vector3 normal = TriangleNormal(vertexAIndex, vertexBIndex, vertexCIndex);
            
            if(vertexAIndex > 0)
            {
                normals[vertexAIndex] += normal;
            }
            if (vertexBIndex > 0)
            {
                normals[vertexBIndex] += normal;
            }
            if (vertexCIndex > 0)
            {
                normals[vertexCIndex] += normal;
            }
        }

        for(int index = 0; index < normals.Length; index++)
        {
            normals[index].Normalize();
        }

        return normals;
    }

    private Vector3 TriangleNormal(int indexA, int indexB, int indexC)
    {
        Vector3 vertexA = indexA < 0 ? outsideVertices[-indexA - 1] : vertices[indexA];
        Vector3 vertexB = indexB < 0 ? outsideVertices[-indexB - 1] : vertices[indexB];
        Vector3 vertexC = indexC < 0 ? outsideVertices[-indexC - 1] : vertices[indexC];

        Vector3 edgeAB = vertexB - vertexA;
        Vector3 edgeAC = vertexC - vertexA;

        return Vector3.Cross(edgeAB, edgeAC).normalized;
    }
}