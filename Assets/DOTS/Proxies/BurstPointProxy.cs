﻿using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using System;
[RequireComponent(typeof(FireDurationComponentProxy))]
public class BurstPointProxy : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    public GameObject Prefab;
    public float ShootRate = .2f;
    public float Speed = 50f;
    public bool IsPlayer = false;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new BurstPointComponent
        {
            Bullet = conversionSystem.GetPrimaryEntity(Prefab),
            ShootRate = ShootRate,
            Speed = Speed,
        };
        dstManager.AddComponentData(entity, data);
    }
    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(Prefab);
    }
}
