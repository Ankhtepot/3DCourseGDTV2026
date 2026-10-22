using System;
using DefaultNamespace;
using Unity.Collections;
using UnityEngine;

namespace _1_Obstacle_Dodge.Scripts
{
    public class Scorer : MonoBehaviour
    {
        [SerializeField][ReadOnly] private int score;
        
        private void OnCollisionEnter(Collision collision)
        {
            if(!collision.gameObject.IsInLayerMask(Constants.ObstacleLayer) || 
                collision.gameObject.CompareTag(Constants.TaggedTag))
                return;
            
            score++;
            
            collision.gameObject.tag = Constants.TaggedTag;
            
            Debug.Log($"You bumped into a thing this many times: {score}. Object name: {collision.gameObject.name}");
        }
    }
}