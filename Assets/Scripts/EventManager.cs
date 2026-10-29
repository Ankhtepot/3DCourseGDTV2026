using UnityEngine.Events;

namespace DefaultNamespace
{
    public static class EventManager
    {
        //------------ Subscribers ------------
        // AI 2 Jungle Course Project
        public static event UnityAction OnPlayerReachedCourseObjective;
        
        //------------ Triggers ------------
        // AI 2 Jungle Course Project
        public static void TriggerOnPlayerReachedCourseObjective() => OnPlayerReachedCourseObjective?.Invoke();
    }
}