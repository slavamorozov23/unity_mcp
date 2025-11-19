import json
from typing import Dict, List, Optional, Any

# Импортируем все модули из пакета
from modules import *

class UnitySceneAPI:
    def __init__(self, host: str = "localhost", port: int = 8080):
        self.base_url = f"http://{host}:{port}"

        # --- Core / Objects ---
        self.hierarchy_module = GetHierarchyModule(self.base_url)
        self.components_module = GetComponentsModule(self.base_url)
        self.create_object_module = CreateObjectModule(self.base_url)
        self.delete_object_module = DeleteObjectModule(self.base_url)
        self.modify_component_module = ModifyComponentModule(self.base_url)
        self.add_component_module = AddComponentModule(self.base_url)
        self.remove_component_module = RemoveComponentModule(self.base_url)
        self.find_objects_module = FindObjectsModule(self.base_url)
        self.move_object_module = MoveObjectModule(self.base_url)
        self.reset_object_module = ResetObjectModule(self.base_url)
        self.rename_object_module = RenameObjectModule(self.base_url)
        self.set_active_module = SetObjectActiveModule(self.base_url)
        
        # --- Scene Management ---
        self.scene_management_module = SceneManagementModule(self.base_url)
        
        # --- Prefabs ---
        self.create_object_from_prefab_module = CreateObjectFromPrefabModule(self.base_url)
        self.save_object_as_prefab_module = SaveObjectAsPrefabModule(self.base_url)
        self.instantiate_prefab_module = InstantiatePrefabModule(self.base_url)
        self.get_prefabs_list_module = GetPrefabsListModule(self.base_url)

        # --- Helpers / NLP ---
        self.nlp_search_module = NLPSearchModule(self.base_url)
        self.object_picker_module = ObjectPickerModule(self.base_url)
        self.picker_component_variants_module = PickerComponentVariantsModule(self.base_url)
        self.logging_module = LoggingModule()

        # --- Stage 1: Debug & Vision (Smart) ---
        self.snapshot_module = GameViewSnapshotModule(self.base_url)
        self.tilemap_snapshot_module = TilemapSnapshotModule(self.base_url)
        self.get_logs_module = GetLogsModule(self.base_url)
        self.search_logs_module = SearchLogsModule(self.base_url)
        self.scene_status_module = GetSceneStatusModule(self.base_url)
        self.play_control_module = PlayControlModule(self.base_url)

        # --- Stage 2: Assets & Files ---
        self.get_templates_module = GetCreationTemplatesModule(self.base_url)
        self.create_template_module = CreateFromTemplateModule(self.base_url)
        self.get_asset_info_module = GetAssetInfoModule(self.base_url)
        self.modify_asset_module = ModifyAssetModule(self.base_url)
        self.asset_picker_module = GetAssetPickerOptionsModule(self.base_url)

        # --- Stage 3: Tilemaps ---
        self.get_tilemaps_module = GetTilemapsModule(self.base_url)
        self.paint_tile_module = PaintTileModule(self.base_url)
        self.manage_tile_asset_module = ManageTileAssetModule(self.base_url)

        # --- Stage 4: Animation & Input ---
        self.get_anim_info_module = GetAnimInfoModule(self.base_url)
        self.manage_anim_module = ManageAnimPropertyModule(self.base_url)
        self.manage_input_module = ManageInputAxisModule(self.base_url)
        self.get_input_constants_module = GetInputConstantsModule(self.base_url)

    # ==========================================
    # Основной диспетчер команд (Unified JSON Interface)
    # ==========================================
    def execute_command(self, command: Dict) -> Dict:
        """
        Единая точка входа для выполнения команд.
        Принимает: {"action": "name", "params": {...}}
        """
        try:
            action = command.get("action")
            params = command.get("params", {})
            
            # Логирование входящего запроса (опционально)
            # self.logging_module.log_structured(command, {}) 

            result = None

            # --- Core: Objects ---
            if action == "get_hierarchy":
                result = self.hierarchy_module.execute(params)
            elif action == "get_components":
                result = self.components_module.execute(params.get("object_path", ""))
            elif action == "create_object":
                result = self.create_object_module.execute(params.get("name", "GameObject"), params.get("parent_path", ""))
            elif action == "delete_object":
                result = self.delete_object_module.execute(params.get("object_path", ""))
            elif action == "modify_component":
                result = self.modify_component_module.execute(
                    params.get("object_path", ""), 
                    params.get("component_type", ""), 
                    params.get("properties", {})
                )
            elif action == "add_component":
                result = self.add_component_module.execute(params.get("object_path", ""), params.get("component_type", ""))
            elif action == "remove_component":
                result = self.remove_component_module.execute(params.get("object_path", ""), params.get("component_type", ""))
            elif action == "find_objects":
                result = self.find_objects_module.execute(params.get("name", ""))
            elif action == "move_object":
                result = self.move_object_module.execute(
                    params.get("source_path", ""), 
                    params.get("target_parent_path", ""), 
                    params.get("new_name", "")
                )
            elif action == "reset_object":
                result = self.reset_object_module.execute(params.get("object_path", ""))
            elif action == "rename_object":
                result = self.rename_object_module.execute(params.get("path", ""), params.get("new_name", ""))
            elif action == "set_active":
                result = self.set_active_module.execute(params.get("path", ""), params.get("active", True))

            # --- Core: Scenes ---
            elif action == "open_scene":
                result = self.scene_management_module.open_scene(params.get("scene_path", ""))
            elif action == "get_build_scenes":
                result = self.scene_management_module.get_build_scenes()
            
            # --- Core: Prefabs ---
            elif action == "create_from_prefab":
                result = self.create_object_from_prefab_module.execute(
                    params.get("prefab_path", ""), 
                    params.get("object_name", ""), 
                    params.get("parent_path", "")
                )
            elif action == "save_as_prefab":
                result = self.save_object_as_prefab_module.execute(
                    params.get("object_path", ""), 
                    params.get("prefab_name", ""), 
                    params.get("target_folder", "Assets/Prefabs")
                )
            elif action == "get_prefabs":
                result = self.get_prefabs_list_module.execute()

            # --- Core: NLP & Pickers ---
            elif action == "nlp_search":
                result = self.nlp_search_module.execute(params.get("query", ""))
            elif action == "object_picker_options":
                result = self.object_picker_module.get_object_picker_options(params.get("object_path", ""))
            elif action == "component_variants":
                result = self.picker_component_variants_module.execute(
                    params.get("object_path", ""),
                    params.get("component_type", ""),
                    params.get("param_name", ""),
                    params.get("query", "")
                )

            # --- Vision & Debug (Smart) ---
            elif action == "get_snapshot":
                # "Умный" снимок с Raycast оптимизацией
                result = self.snapshot_module.execute(
                    target_paths=params.get("targetPaths", []),
                    distance=params.get("distance", 10.0),
                    width=params.get("width", 1280), 
                    height=params.get("height", 720)
                )
            elif action == "get_tilemap_snapshot":
                # "Умный" снимок тайлмапа с анализом наложения объектов и Z-координат
                result = self.tilemap_snapshot_module.execute(
                    tilemap_name=params.get("tilemap_name", ""),
                    overlay_object_paths=params.get("overlayObjectPaths", []),
                    center_x=params.get("center_x", 0), 
                    center_y=params.get("center_y", 0),
                    width=params.get("width", 20), 
                    height=params.get("height", 20)
                )
            elif action == "get_logs":
                result = self.get_logs_module.execute()
            elif action == "search_logs":
                result = self.search_logs_module.execute(params.get("query", ""), params.get("max_results", 100))
            elif action == "scene_status":
                result = self.scene_status_module.execute()
            elif action == "scene_control":
                result = self.play_control_module.execute(params.get("command", "stop"))

            # --- Assets ---
            elif action == "search_templates":
                result = self.get_templates_module.execute(params.get("query", ""))
            elif action == "create_asset":
                result = self.create_template_module.execute(
                    params.get("template_name", ""), 
                    params.get("target_path", ""), 
                    params.get("file_name", "")
                )
            elif action == "get_asset_info":
                result = self.get_asset_info_module.execute(params.get("asset_path", ""))
            elif action == "modify_asset":
                result = self.modify_asset_module.execute(
                    params.get("asset_path", ""),
                    params.get("properties"),
                    params.get("import_settings")
                )
            elif action == "asset_picker_options":
                result = self.asset_picker_module.execute(
                    params.get("asset_path", ""),
                    params.get("property_name", "")
                )

            # --- Tilemaps ---
            elif action == "get_tilemaps":
                result = self.get_tilemaps_module.execute()
            elif action == "paint_tile":
                result = self.paint_tile_module.execute(
                    params.get("tilemap_name", ""),
                    params.get("tile_name", ""), # None/empty for erase
                    params.get("x", 0),
                    params.get("y", 0)
                )
            elif action == "manage_tile_asset":
                if params.get("sub_action") == "create":
                    result = self.manage_tile_asset_module.create_tile(
                        params.get("tile_name", ""), params.get("sprite_path", "")
                    )
                else:
                    result = self.manage_tile_asset_module.delete_tile(params.get("tile_name", ""))

            # --- Animation & Input ---
            elif action == "get_anim_info":
                result = self.get_anim_info_module.execute(params.get("anim_path", ""), params.get("query", ""))
            elif action == "modify_anim":
                sub = params.get("sub_action")
                if sub == "add_key":
                    result = self.manage_anim_module.add_key(
                        params.get("anim_path", ""), params.get("object_path", ""),
                        params.get("component_type", ""), params.get("property_name", ""),
                        params.get("time", 0.0), params.get("value", 0.0)
                    )
                elif sub == "remove_property":
                    result = self.manage_anim_module.remove_property(
                        params.get("anim_path", ""), params.get("object_path", ""),
                        params.get("component_type", ""), params.get("property_name", "")
                    )
            elif action == "manage_input":
                sub = params.get("sub_action")
                if sub == "list":
                    result = self.manage_input_module.list_axes()
                elif sub == "delete":
                    result = self.manage_input_module.delete_axis(params.get("name", ""))
                elif sub == "create":
                    result = self.manage_input_module.create_axis(
                        params.get("name", ""), params.get("pos_btn", ""), params.get("neg_btn", ""),
                        params.get("alt_pos", ""), params.get("alt_neg", ""),
                        params.get("type", 0), params.get("axis", 0)
                    )
                elif sub == "constants":
                    # Получение подсказанных констант (Axis, JoyNum и т.д.)
                    result = self.get_input_constants_module.execute()

            else:
                result = {"success": False, "error": f"Unknown action: {action}"}

            # Логируем результат
            self.logging_module.log_structured(command, result if result else {})
            return result

        except Exception as e:
            err = {"success": False, "action": action, "error": str(e)}
            self.logging_module.log_structured(command, err)
            return err

    def get_log_path(self) -> str:
        return self.logging_module.get_log_file_path()

if __name__ == "__main__":
    api = UnitySceneAPI()
    
    print("--- Testing Connection & Hierarchy ---")
    print(json.dumps(api.execute_command({"action": "get_hierarchy"}), indent=2))
    
    print("\n--- Testing Input Constants ---")
    print(json.dumps(api.execute_command({"action": "manage_input", "params": {"sub_action": "constants"}}), indent=2))