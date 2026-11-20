using System.Collections.Generic;
using UnityEngine;

public partial class StoryCutsceneDirector
{
    [Header("Companion navigation")]
    [Min(.2f)] public float companionPathCell = .6f;
    [Min(.05f)] public float companionClearance = .22f;
    [Min(.1f)] public float companionStepHeight = .35f;
    [Min(0)] public float companionWanderRadius = 2f;
    class CompanionRoute
    {
        public List<Vector3> points = new List<Vector3>();
        public float nextPlan, nextWander;
        public Vector3 wander;
    }
    readonly Dictionary<Transform, CompanionRoute> companionRoutes = new Dictionary<Transform, CompanionRoute>();
    Vector3 navigationPlayerLast;
    float playerIdleSeconds;

    bool IsCompanionCollider(Collider collider)
    {
        if (existingPlayer != null && collider.transform.IsChildOf(existingPlayer)) return true;
        foreach (var friend in friends.Values)
            if (friend != null && collider.transform.IsChildOf(friend)) return true;
        return false;
    }

    bool NavigationGround(Vector3 at, out Vector3 feet)
    {
        feet = at;
        float best = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(at + Vector3.up * (companionStepHeight + .1f),
            Vector3.down, companionStepHeight * 2 + .25f, ~0, QueryTriggerInteraction.Ignore))
            if (!IsCompanionCollider(hit.collider) && hit.normal.y >= .7f && hit.point.y > best)
                best = hit.point.y;
        if (float.IsNegativeInfinity(best) || Mathf.Abs(best-at.y) > companionStepHeight) return false;
        feet.y = best + actorGroundClearance;
        float height = Mathf.Max(.7f, lastPlayerVisualSize.y);
        foreach (var hit in Physics.OverlapCapsule(feet + Vector3.up * (companionClearance+.06f),
            feet + Vector3.up * Mathf.Max(companionClearance+.06f, height-companionClearance),
            companionClearance, ~0, QueryTriggerInteraction.Ignore))
            if (!IsCompanionCollider(hit)) return false;
        return true;
    }

    bool NavigationEdge(Vector3 from, Vector3 to)
    {
        Vector3 delta = to-from;
        float height = Mathf.Max(.7f,lastPlayerVisualSize.y);
        foreach (var hit in Physics.CapsuleCastAll(from+Vector3.up*(companionStepHeight+companionClearance+.03f),
            from+Vector3.up*Mathf.Max(companionStepHeight+companionClearance+.03f,height-companionClearance),
            companionClearance,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            if (!IsCompanionCollider(hit.collider) && hit.normal.y < .7f) return false;
        return true;
    }

    // Bounded local A*: samples real collision and height, never teleports a follower.
    List<Vector3> FindCompanionPath(Vector3 start, Vector3 goal)
    {
        float cell = Mathf.Max(.25f,companionPathCell);
        var zero = Vector2Int.zero;
        var open = new List<Vector2Int>{zero};
        var closed = new HashSet<Vector2Int>();
        var positions = new Dictionary<Vector2Int,Vector3>{{zero,start}};
        var costs = new Dictionary<Vector2Int,float>{{zero,0}};
        var parents = new Dictionary<Vector2Int,Vector2Int>();
        var closest = zero;
        float closestDistance = Vector3.ProjectOnPlane(start-goal,Vector3.up).sqrMagnitude;
        for(int iteration=0;open.Count>0 && iteration<700;iteration++)
        {
            int best=0;float score=float.PositiveInfinity;
            for(int i=0;i<open.Count;i++)
            {
                float f=costs[open[i]]+Vector3.ProjectOnPlane(positions[open[i]]-goal,Vector3.up).magnitude;
                if(f<score){score=f;best=i;}
            }
            var current=open[best];open.RemoveAt(best);closed.Add(current);
            Vector3 p=positions[current];
            float distance = Vector3.ProjectOnPlane(p-goal,Vector3.up).sqrMagnitude;
            if (distance < closestDistance) { closest = current; closestDistance = distance; }
            if(Vector3.ProjectOnPlane(p-goal,Vector3.up).magnitude <= cell)
            {
                var path=new List<Vector3>();
                while(current!=zero){path.Add(positions[current]);current=parents[current];}
                path.Reverse(); return path;
            }
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                if(x==0&&z==0)continue;
                var next=current+new Vector2Int(x,z);
                if(closed.Contains(next) || Mathf.Abs(next.x)>24 || Mathf.Abs(next.y)>24)continue;
                Vector3 sample=new Vector3(start.x+next.x*cell,p.y,start.z+next.y*cell);
                if(!NavigationGround(sample,out var q) || !NavigationEdge(p,q))continue;
                float cost=costs[current]+Vector3.Distance(p,q);
                if(costs.TryGetValue(next,out float old)&&old<=cost)continue;
                costs[next]=cost;positions[next]=q;parents[next]=current;
                if(!open.Contains(next))open.Add(next);
            }
        }
        // Keep progressing when a sprinting player is outside this local grid.
        var partial = new List<Vector3>();
        while (closest != zero) { partial.Add(positions[closest]); closest = parents[closest]; }
        partial.Reverse();
        return partial;
    }

    Vector3 CompanionWaypoint(Transform actor, Vector3 target, bool canWander)
    {
        if(!companionRoutes.TryGetValue(actor,out var route))
        { route=new CompanionRoute {nextWander=Time.time+Random.Range(4f,8f)};companionRoutes.Add(actor,route); }
        var visual=actor.GetComponentInChildren<Renderer>();
        Vector3 feet=actor.position;feet.y=visual!=null?visual.bounds.min.y:feet.y;
        if(canWander && companionWanderRadius>0 && Vector3.ProjectOnPlane(feet-target,Vector3.up).sqrMagnitude < 16)
        {
            if(Time.time>=route.nextWander)
            {
                Vector2 offset=Random.insideUnitCircle*companionWanderRadius;
                route.wander=target+new Vector3(offset.x,0,offset.y);
                route.nextWander=Time.time+Random.Range(5f,10f);route.nextPlan=0;
            }
            if(route.wander!=Vector3.zero)target=route.wander;
        }
        else route.wander=Vector3.zero;
        if(Time.time>=route.nextPlan)
        {route.points=FindCompanionPath(feet,target);route.nextPlan=Time.time+Random.Range(.8f,1.2f);}
        while(route.points.Count>0 && Vector3.ProjectOnPlane(route.points[0]-feet,Vector3.up).sqrMagnitude<.08f)
            route.points.RemoveAt(0);
        return route.points.Count>0 ? route.points[0]+Vector3.up*(actor.position.y-feet.y) : actor.position;
    }
}
