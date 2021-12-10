using Colyseus.Schema;
using Colyseus;

public class ArenaRoomState : ColyseusRoomState
{
	[Type(0, "map", typeof(MapSchema<ArenaNetworkedEntity>))]
	public MapSchema<ArenaNetworkedEntity> networkedEntities = new MapSchema<ArenaNetworkedEntity>();
	[Type(1, "map", typeof(MapSchema<ArenaNetworkedUser>))]
	public MapSchema<ArenaNetworkedUser> networkedUsers = new MapSchema<ArenaNetworkedUser>();
	[Type(2, "map", typeof(MapSchema<string>), "string")]
	public MapSchema<string> attributes = new MapSchema<string>();

}

namespace Colyseus
{
	public class ColyseusRoomState : Schema.Schema
	{ 
		
	}
}