"""
Unity Scene API Modules Package.
Exposes all agent capabilities including Smart Vision and Input.
"""
from .common import HTTPClient
from .get_hierarchy import GetHierarchyModule
from .nlp import NLPSearchModule
from .logging import LoggingModule

# Core Modules
from .get_components_module import GetComponentsModule
from .create_object_module import CreateObjectModule
from .delete_object_module import DeleteObjectModule
from .modify_component_module import ModifyComponentModule
from .add_component_module import AddComponentModule
from .remove_component_module import RemoveComponentModule
from .find_objects_module import FindObjectsModule
from .scene_management_module import SceneManagementModule
from .object_picker_module import ObjectPickerModule
from .picker_component_variants_module import PickerComponentVariantsModule
from .create_object_from_prefab_module import CreateObjectFromPrefabModule
from .save_object_as_prefab_module import SaveObjectAsPrefabModule
from .instantiate_prefab_module import InstantiatePrefabModule
from .get_prefabs_list_module import GetPrefabsListModule
from .move_object_module import MoveObjectModule
from .reset_object_module import ResetObjectModule
from .rename_object_module import RenameObjectModule
from .set_object_active_module import SetObjectActiveModule

# Stage 1: Vision & Debug (Smart)
from .game_view_snapshot_module import GameViewSnapshotModule
from .tilemap_snapshot_module import TilemapSnapshotModule
from .get_logs_module import GetLogsModule
from .search_logs_module import SearchLogsModule
from .get_scene_status_module import GetSceneStatusModule
from .play_control_module import PlayControlModule

# Stage 2: Assets & Files
from .get_creation_templates_module import GetCreationTemplatesModule
from .create_from_template_module import CreateFromTemplateModule
from .get_asset_info_module import GetAssetInfoModule
from .modify_asset_module import ModifyAssetModule
from .get_asset_picker_options_module import GetAssetPickerOptionsModule

# Stage 3: Tilemaps
from .get_tilemaps_module import GetTilemapsModule
from .paint_tile_module import PaintTileModule
from .manage_tile_asset_module import ManageTileAssetModule

# Stage 4: Animation & Input
from .get_anim_info_module import GetAnimInfoModule
from .manage_anim_property_module import ManageAnimPropertyModule
from .manage_input_axis_module import ManageInputAxisModule
from .get_input_constants_module import GetInputConstantsModule

__all__ = [
    "HTTPClient",
    "GetHierarchyModule", "NLPSearchModule", "LoggingModule",
    "GetComponentsModule", "CreateObjectModule", "DeleteObjectModule",
    "ModifyComponentModule", "AddComponentModule", "RemoveComponentModule",
    "FindObjectsModule", "SceneManagementModule", "ObjectPickerModule",
    "PickerComponentVariantsModule", "CreateObjectFromPrefabModule",
    "SaveObjectAsPrefabModule", "InstantiatePrefabModule", "GetPrefabsListModule",
    "MoveObjectModule", "ResetObjectModule", "RenameObjectModule", "SetObjectActiveModule",
    "GameViewSnapshotModule", "TilemapSnapshotModule", "GetLogsModule",
    "SearchLogsModule", "GetSceneStatusModule", "PlayControlModule",
    "GetCreationTemplatesModule", "CreateFromTemplateModule", "GetAssetInfoModule",
    "ModifyAssetModule", "GetAssetPickerOptionsModule",
    "GetTilemapsModule", "PaintTileModule", "ManageTileAssetModule",
    "GetAnimInfoModule", "ManageAnimPropertyModule", "ManageInputAxisModule",
    "GetInputConstantsModule"
]