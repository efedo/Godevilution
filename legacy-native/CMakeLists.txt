include(Definitions)
include(functions/devilutionx_library)
include(functions/genex)

if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  # protoc 3.12-3.14 (for example Ubuntu 22.04) accepts proto3 optional fields only behind a flag.
  set(DEVILUTIONX_PROTOC_OPTIONS)
  if(Protobuf_VERSION AND Protobuf_VERSION VERSION_LESS 3.15)
    if(CMAKE_VERSION VERSION_LESS 3.28)
      message(FATAL_ERROR "Protobuf ${Protobuf_VERSION} needs CMake 3.28 or newer to pass --experimental_allow_proto3_optional")
    endif()
    set(DEVILUTIONX_PROTOC_OPTIONS PROTOC_OPTIONS --experimental_allow_proto3_optional)
  endif()
  protobuf_generate(
    APPEND_PATH
    LANGUAGE cpp
    OUT_VAR DEVILUTIONX_PROTOCOL_GENERATED
    EXPORT_MACRO DVL_PROTOBUF_EXPORT
    PROTOC_EXE "${Protobuf_PROTOC_EXECUTABLE}"
    ${DEVILUTIONX_PROTOC_OPTIONS}
    PROTOS "${PROJECT_SOURCE_DIR}/protocol/devilution.proto")
  foreach(DEVILUTIONX_PROTOCOL_GENERATED_FILE IN LISTS DEVILUTIONX_PROTOCOL_GENERATED)
    if(DEVILUTIONX_PROTOCOL_GENERATED_FILE MATCHES "\\.cc$")
      list(APPEND DEVILUTIONX_PROTOCOL_SRCS "${DEVILUTIONX_PROTOCOL_GENERATED_FILE}")
    else()
      list(APPEND DEVILUTIONX_PROTOCOL_HDRS "${DEVILUTIONX_PROTOCOL_GENERATED_FILE}")
    endif()
  endforeach()
endif()

set(libdevilutionx_SRCS
  application/fatal_error.cpp
  application/debug_commands.cpp
  ui/automap.cpp
  application/capture.cpp
  engine/cursor.cpp
  game/monsters/monster_corpses.cpp
  application/debug.cpp
  ui/debug_overlay/imgui_overlay.cpp
  application/diablo.cpp
  ui/diablo_msg.cpp
  game/quests/quest_stars.cpp
  game/events/event_bus.cpp
  game/replay/replay.cpp
  game/replay/replay_fixture.cpp
  ui/gamemenu.cpp
  ui/menu_framework.cpp
  ui/help.cpp
  engine/hwcursor.cpp
  ui/load_screens.cpp
  game/players/inventory.cpp
  persistence/loadsave.cpp
  ui/menu.cpp
  ui/minitext.cpp
  game/missiles/missiles.cpp
  ui/movie.cpp
  network/protocol/msg.cpp
  network/protocol/command_delivery.cpp
  network/protocol/network_ticks.cpp
  persistence/save_file.cpp
  ui/player_messages.cpp
  game/portals/portal.cpp
  application/restrict.cpp
  network/protocol/sync.cpp
  network/protocol/chat_messages.cpp
  game/towners/towners.cpp
  engine/track.cpp

  ui/panel/control_chat.cpp
  ui/panel/control_chat_commands.cpp
  ui/panel/control_flasks.cpp
  ui/panel/control_gold.cpp
  ui/panel/control_infobox.cpp
  ui/panel/control_panel.cpp

  controls/axis_direction.cpp
  controls/controller_motion.cpp
  controls/controller.cpp
  controls/devices/joystick.cpp
  controls/devices/kbcontroller.cpp
  controls/game_controls.cpp
  controls/keymapper.cpp
  controls/menu_controls.cpp
  controls/modifier_hints.cpp
  controls/player_controls.cpp

  ui/menu/button.cpp
  ui/menu/credits.cpp
  ui/menu/credits_lines.cpp
  ui/menu/diabloui.cpp
  ui/menu/dialogs.cpp
  ui/menu/hero/hero_selection.cpp
  ui/menu/mainmenu.cpp
  ui/menu/multi/connection_selection.cpp
  ui/menu/multi/game_selection.cpp
  ui/menu/progress.cpp
  ui/menu/scrollbar.cpp
  ui/menu/ok_dialog.cpp
  ui/menu/startup_selection.cpp
  ui/menu/yes_no_dialog.cpp
  ui/menu/settingsmenu.cpp
  ui/menu/support_lines.cpp
  ui/menu/title.cpp
  ui/menu/text_input.cpp

  data/game_data_manager.cpp
  data/content_manifest.cpp
  data/mod_manager.cpp

  network/transport/abstract_net.cpp
  network/transport/base.cpp
  network/transport/cdwrap.cpp
  network/transport/frame_queue.cpp
  network/transport/loopback.cpp
  network/transport/packet.cpp

  engine/actor_position.cpp
  engine/animationinfo.cpp
  engine/gfx/backbuffer_state.cpp
  engine/bestiary.cpp
  engine/gfx/graphics_pipeline.cpp
  engine/sdl_events.cpp
  engine/gfx/palette.cpp
  engine/audio/sound_position.cpp
  engine/gfx/translation_table.cpp
  engine/world.cpp

  engine/render/automap_render.cpp
  engine/render/world_renderer.cpp

  game/items/validation.cpp

  game/levels/level.cpp
  game/levels/reencode_dun_cels.cpp
  game/levels/setmaps.cpp
  game/levels/themes.cpp
  game/levels/tile_properties.cpp
  game/levels/town.cpp
  game/levels/triggers.cpp

  lua/autocomplete.cpp
  lua/lua_event.cpp
  lua/lua_global.cpp
  lua/modules/audio.cpp
  lua/modules/dev.cpp
  lua/modules/dev/display.cpp
  lua/modules/dev/items.cpp
  lua/modules/dev/level.cpp
  lua/modules/dev/level/map.cpp
  lua/modules/dev/level/warp.cpp
  lua/modules/dev/monsters.cpp
  lua/modules/dev/player.cpp
  lua/modules/dev/player/gold.cpp
  lua/modules/dev/player/spells.cpp
  lua/modules/dev/player/stats.cpp
  lua/modules/dev/quests.cpp
  lua/modules/dev/search.cpp
  lua/modules/dev/towners.cpp
  lua/modules/floatingnumbers.cpp
  lua/modules/i18n.cpp
  lua/modules/items.cpp
  lua/modules/log.cpp
  lua/modules/monsters.cpp
  lua/modules/player.cpp
  lua/modules/render.cpp
  lua/modules/system.cpp
  lua/modules/towners.cpp
  lua/repl.cpp

  game/monsters/validation.cpp

  ui/panel/charpanel.cpp
  ui/panel/console.cpp
  ui/panel/info_box.cpp
  ui/panel/mainpanel.cpp
  ui/panel/partypanel.cpp
  ui/panel/spell_book.cpp
  ui/panel/spell_icons.cpp
  ui/panel/spell_list.cpp

  platform/locale.cpp

  game/players/validation.cpp

  game/portals/validation.cpp
  game/objects/object_manager.cpp

  qol/autopickup.cpp
  qol/chatlog.cpp
  qol/floatingnumbers.cpp
  qol/itemlabels.cpp
  qol/monhealthbar.cpp
  qol/stash.cpp
  qol/visual_store.cpp
  qol/xpbar.cpp

  game/quests/validation.cpp

  network/storm/storm_net.cpp
  network/storm/storm_svid.cpp

  tables/misdat.cpp
  tables/textdat.cpp
  tables/townerdat.cpp

  utils/display.cpp
  utils/language.cpp
  utils/sdl/sdl_bilinear_scale.cpp
  utils/sdl/sdl_thread.cpp
  utils/image/surface_to_clx.cpp
  utils/timer.cpp)

# These files are responsible for most of the runtime in Debug mode.
# Apply some optimizations to them even in Debug mode to get reasonable performance.
#
# They also perform better with -O2 rather than -O3 even in Release mode.
set(_optimize_in_debug_srcs
  engine/render/clx_render.cpp
  engine/render/dun_render.cpp
  engine/render/text_render.cpp
  utils/image/cel_to_clx.cpp
  utils/image/cl2_to_clx.cpp
  utils/image/pcx_to_clx.cpp)
if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
  set_source_files_properties(${_optimize_in_debug_srcs} PROPERTIES COMPILE_OPTIONS "-O2;--param=max-vartrack-size=900000000")
elseif(CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
  set_source_files_properties(${_optimize_in_debug_srcs} PROPERTIES COMPILE_OPTIONS "-O2")
endif()

# We need to define all the object libraries first
# because our workaround for transitive dependency support
# requires targets to exist when calling `target_link_dependencies`
# (see object_libraries.cmake).

add_devilutionx_object_library(libdevilutionx_assets
  engine/assets.cpp
)
target_link_dependencies(libdevilutionx_assets PUBLIC
  DevilutionX::SDL
  fmt::fmt
  tl
  libdevilutionx_headless_mode
  libdevilutionx_game_mode
  libdevilutionx_mpq
  libdevilutionx_paths
  libdevilutionx_sdl2_to_1_2_backports
  libdevilutionx_strings
  ${DEVILUTIONX_PLATFORM_ASSETS_LINK_LIBRARIES}
)

add_devilutionx_object_library(libdevilutionx_cel_to_clx
  utils/image/cel_to_clx.cpp
)
target_link_dependencies(libdevilutionx_cel_to_clx
  PRIVATE
  libdevilutionx_endian_write
)

add_devilutionx_object_library(libdevilutionx_cl2_to_clx
  utils/image/cl2_to_clx.cpp
)
target_link_dependencies(libdevilutionx_cl2_to_clx
  PRIVATE
  libdevilutionx_endian_write
)

add_devilutionx_object_library(libdevilutionx_clx_render
  engine/render/clx_render.cpp
)
target_link_dependencies(libdevilutionx_clx_render PUBLIC
  DevilutionX::SDL
  fmt::fmt
  libdevilutionx_light_render
  libdevilutionx_palette_blending
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_codec
  utils/crypto/codec.cpp
  utils/crypto/sha.cpp
)
target_link_dependencies(libdevilutionx_codec PRIVATE
  DevilutionX::SDL
  libdevilutionx_log
)

add_devilutionx_object_library(libdevilutionx_controller_buttons
  controls/controller_buttons.cpp
)
target_link_dependencies(libdevilutionx_controller_buttons
  DevilutionX::SDL
)

add_devilutionx_object_library(libdevilutionx_debug_console_history
  ui/debug_overlay/console_history.cpp
)

add_devilutionx_object_library(libdevilutionx_debug_overlay_state
  ui/debug_overlay/overlay_state.cpp
)

add_devilutionx_object_library(libdevilutionx_control_mode
  controls/control_mode.cpp
)
target_link_dependencies(libdevilutionx_control_mode PUBLIC
  libdevilutionx_controller_buttons
)

add_devilutionx_object_library(libdevilutionx_padmapper
  controls/padmapper.cpp
)
target_link_dependencies(libdevilutionx_padmapper PUBLIC
  libdevilutionx_controller_buttons
  libdevilutionx_options
)

add_devilutionx_object_library(libdevilutionx_palette_kd_tree
  utils/palette_kd_tree.cpp
)
target_link_dependencies(libdevilutionx_palette_kd_tree PUBLIC
  DevilutionX::SDL
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_paths
  utils/paths.cpp
)
target_link_dependencies(libdevilutionx_paths PUBLIC
  DevilutionX::SDL
  libdevilutionx_file_util
  libdevilutionx_log
  libdevilutionx_sdl2_to_1_2_backports
)

add_devilutionx_object_library(libdevilutionx_pcx_to_clx
  utils/image/pcx_to_clx.cpp
)
target_link_dependencies(libdevilutionx_pcx_to_clx
  PUBLIC
  DevilutionX::SDL
  libdevilutionx_assets
  libdevilutionx_endian_write
)

add_devilutionx_object_library(libdevilutionx_primitive_render
  engine/render/primitive_render.cpp
)
target_link_dependencies(libdevilutionx_primitive_render
  PUBLIC
  libdevilutionx_palette_blending
  libdevilutionx_surface
)

add_devilutionx_object_library(libdevilutionx_crawl
  engine/crawl.cpp
)
target_link_dependencies(libdevilutionx_crawl PUBLIC
  tl
)

add_devilutionx_object_library(libdevilutionx_direction
  engine/math/direction.cpp
)

add_devilutionx_object_library(libdevilutionx_dun_render
  engine/render/dun_render.cpp
)
target_link_libraries(libdevilutionx_dun_render
  PUBLIC
  DevilutionX::SDL
  libdevilutionx_light_render
  libdevilutionx_surface
  PRIVATE
  libdevilutionx_options
)

add_library(libdevilutionx_endian_write INTERFACE)
target_link_libraries(libdevilutionx_endian_write INTERFACE
  DevilutionX::SDL
)

add_devilutionx_object_library(libdevilutionx_surface
  engine/gfx/surface.cpp
)
target_link_dependencies(libdevilutionx_surface PUBLIC
  DevilutionX::SDL
)

add_devilutionx_object_library(libdevilutionx_file_util
  utils/file/file_util.cpp
)
target_link_dependencies(libdevilutionx_file_util PRIVATE
  DevilutionX::SDL
  libdevilutionx_log
  ${DEVILUTIONX_PLATFORM_FILE_UTIL_LINK_LIBRARIES}
)

add_devilutionx_object_library(libdevilutionx_format_int
  utils/string/format_int.cpp
)
target_link_dependencies(libdevilutionx_format_int PUBLIC
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_game_mode
  application/game_mode.cpp
)
target_link_dependencies(libdevilutionx_game_mode PRIVATE
  tl
  magic_enum::magic_enum
  libdevilutionx_options
)

add_devilutionx_object_library(libdevilutionx_dungeon_common
  tables/leveldat.cpp
  game/levels/crypt.cpp
  game/levels/level_l1.cpp
  game/levels/level_l2.cpp
  game/levels/level_l3.cpp
  game/levels/level_l4.cpp
  game/levels/dungeon_common.cpp
)
target_link_dependencies(libdevilutionx_dungeon_common PUBLIC
  DevilutionX::SDL
  fmt::fmt
  tl
  libdevilutionx_assets
  libdevilutionx_items
  libdevilutionx_monster
  libdevilutionx_random
  libdevilutionx_txtdata
)

add_devilutionx_object_library(libdevilutionx_headless_mode
  application/headless_mode.cpp
)

add_devilutionx_object_library(libdevilutionx_init
  application/init.cpp
)
target_link_dependencies(libdevilutionx_init PUBLIC
  magic_enum::magic_enum
  libdevilutionx_assets
  libdevilutionx_config
  libdevilutionx_mpq
  libdevilutionx_options
)

add_devilutionx_object_library(libdevilutionx_load_cel
  engine/load/load_cel.cpp
)
target_link_dependencies(libdevilutionx_load_cel
  PUBLIC
  tl
  PRIVATE
  libdevilutionx_strings
)
if(SUPPORTS_MPQ)
  target_link_dependencies(libdevilutionx_load_cel PRIVATE
    libdevilutionx_mpq
    libdevilutionx_cel_to_clx
  )
else()
  target_link_dependencies(libdevilutionx_load_cel PRIVATE
    libdevilutionx_load_clx
  )
endif()

add_devilutionx_object_library(libdevilutionx_load_cl2
  engine/load/load_cl2.cpp
)
target_link_dependencies(libdevilutionx_load_cl2
  PUBLIC
  tl
  libdevilutionx_endian_write
  PRIVATE
  libdevilutionx_strings
)
if(SUPPORTS_MPQ)
  target_link_dependencies(libdevilutionx_load_cl2 PUBLIC
    libdevilutionx_mpq
    libdevilutionx_cl2_to_clx
  )
else()
  target_link_dependencies(libdevilutionx_load_cl2 PRIVATE
    libdevilutionx_load_clx
  )
endif()

add_devilutionx_object_library(libdevilutionx_load_clx
  engine/load/load_clx.cpp
)
target_link_dependencies(libdevilutionx_load_clx
  PUBLIC
  tl
  PRIVATE
  libdevilutionx_assets
)

add_devilutionx_object_library(libdevilutionx_load_pcx
  engine/load/load_pcx.cpp
)
target_link_dependencies(libdevilutionx_load_pcx
  PRIVATE
  DevilutionX::SDL
  libdevilutionx_sdl2_to_1_2_backports
  libdevilutionx_log
  libdevilutionx_strings
)
if(SUPPORTS_MPQ)
  target_link_dependencies(libdevilutionx_load_pcx PUBLIC
    libdevilutionx_assets
    libdevilutionx_pcx_to_clx
  )
else()
  target_link_dependencies(libdevilutionx_load_pcx PRIVATE
    libdevilutionx_load_clx
  )
endif()

add_devilutionx_object_library(libdevilutionx_light_render
  engine/render/light_render.cpp
)

add_devilutionx_object_library(libdevilutionx_lighting
  engine/lighting.cpp
)
target_link_dependencies(libdevilutionx_lighting PUBLIC
  DevilutionX::SDL
  fmt::fmt
  magic_enum::magic_enum
  tl
  unordered_dense::unordered_dense
  libdevilutionx_vision
)

add_devilutionx_object_library(libdevilutionx_logged_fstream
  utils/file/logged_fstream.cpp
)
target_link_dependencies(libdevilutionx_logged_fstream PUBLIC
  libdevilutionx_file_util
  libdevilutionx_log
)

add_devilutionx_object_library(libdevilutionx_items
  tables/itemdat.cpp
  game/items/items.cpp
  game/items/item_pool.cpp
)
target_link_dependencies(libdevilutionx_items PUBLIC
  DevilutionX::SDL
  sol2::sol2
  tl
  libdevilutionx_headless_mode
  libdevilutionx_sound
  libdevilutionx_spells
  libdevilutionx_stores
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_ini
  utils/ini.cpp
)
target_link_dependencies(libdevilutionx_ini PUBLIC
  fmt::fmt
  tl
  unordered_dense::unordered_dense
  libdevilutionx_strings
  libdevilutionx_utf8
)

# We use an INTERFACE library rather than an OBJECT library
# because `libdevilutionx_log` does not have any sources.
add_library(libdevilutionx_log INTERFACE)
target_include_directories(libdevilutionx_log INTERFACE
  ${PROJECT_SOURCE_DIR}/legacy-native)
target_link_libraries(libdevilutionx_log INTERFACE
  DevilutionX::SDL
  fmt::fmt
)
target_sources(libdevilutionx_log INTERFACE $<TARGET_OBJECTS:libdevilutionx_strings>)

add_devilutionx_object_library(libdevilutionx_level_objects
  tables/objdat.cpp
  game/objects/objects.cpp
  game/objects/object_pool.cpp
)
target_link_dependencies(libdevilutionx_level_objects PUBLIC
  DevilutionX::SDL
  unordered_dense::unordered_dense
  tl
  libdevilutionx_direction
  libdevilutionx_headless_mode
  libdevilutionx_monster
  libdevilutionx_options
  libdevilutionx_player
  libdevilutionx_random
  libdevilutionx_txtdata
)

add_devilutionx_object_library(libdevilutionx_monster
  tables/monstdat.cpp
  game/monsters/monsters.cpp
  game/monsters/monster_pool.cpp
)
target_link_dependencies(libdevilutionx_monster
  PUBLIC
  DevilutionX::SDL
  magic_enum::magic_enum
  sol2::sol2
  tl
  unordered_dense::unordered_dense
  libdevilutionx_game_mode
  libdevilutionx_headless_mode
  libdevilutionx_sound
  libdevilutionx_txtdata
  PRIVATE
  libdevilutionx_cl2_to_clx
)

add_devilutionx_object_library(libdevilutionx_palette_blending
  utils/palette_blending.cpp
)
target_link_dependencies(libdevilutionx_palette_blending PUBLIC
  DevilutionX::SDL
  libdevilutionx_palette_kd_tree
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_parse_int
  utils/parse_int.cpp
)
target_link_dependencies(libdevilutionx_parse_int PUBLIC
  tl
)

if(SUPPORTS_MPQ)
  add_devilutionx_object_library(libdevilutionx_mpq
    data/mpq/mpq_common.cpp
    data/mpq/mpq_reader.cpp
    data/mpq/mpq_sdl_rwops.cpp
    data/mpq/mpq_writer.cpp
  )
  target_link_dependencies(libdevilutionx_mpq PUBLIC
    DevilutionX::SDL
    fmt::fmt
    mpqfs::mpqfs
    libdevilutionx_file_util
  )
else()
  add_library(libdevilutionx_mpq INTERFACE)
endif()

add_devilutionx_object_library(libdevilutionx_multiplayer
  network/protocol/multi.cpp
  persistence/pack.cpp
)
target_link_dependencies(libdevilutionx_multiplayer PUBLIC
  libdevilutionx_config
  libdevilutionx_items
)

add_devilutionx_object_library(libdevilutionx_options
  persistence/options.cpp
)
target_link_dependencies(libdevilutionx_options PUBLIC
  DevilutionX::SDL
  fmt::fmt
  magic_enum::magic_enum
  tl
  unordered_dense::unordered_dense
  libdevilutionx_controller_buttons
  libdevilutionx_control_mode
  libdevilutionx_logged_fstream
  libdevilutionx_quick_messages
  libdevilutionx_strings
  libdevilutionx_ini
)

add_devilutionx_object_library(libdevilutionx_pathfinding
  engine/path.cpp
)
target_link_dependencies(libdevilutionx_pathfinding PUBLIC
  tl
  libdevilutionx_crawl
  libdevilutionx_direction
)

if(SUPPORTS_MPQ OR NOT NONET)
  add_devilutionx_object_library(libdevilutionx_pkware_encrypt
    utils/crypto/encrypt.cpp
  )
  target_link_dependencies(libdevilutionx_pkware_encrypt PUBLIC
    DevilutionX::SDL
    mpqfs::mpqfs
  )
else()
  add_library(libdevilutionx_pkware_encrypt INTERFACE)
endif()

add_devilutionx_object_library(libdevilutionx_player
  game/players/players.cpp
  tables/playerdat.cpp
)
target_link_dependencies(libdevilutionx_player
  PUBLIC
  DevilutionX::SDL
  fmt::fmt
  magic_enum::magic_enum
  sol2::sol2
  tl
  unordered_dense::unordered_dense
  libdevilutionx_game_mode
  PRIVATE
  libdevilutionx_load_cl2
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_quests
  game/quests/quests.cpp
)
target_link_dependencies(libdevilutionx_quests PUBLIC
  libdevilutionx_surface
  libdevilutionx_dungeon_common
)

add_devilutionx_object_library(libdevilutionx_random
  engine/random.cpp
)

add_devilutionx_object_library(libdevilutionx_quick_messages
  ui/quick_messages.cpp
)

add_devilutionx_object_library(libdevilutionx_spells
  tables/spelldat.cpp
  game/spells/spells.cpp
)
target_link_dependencies(libdevilutionx_spells PUBLIC
  tl
  libdevilutionx_player
  libdevilutionx_txtdata
)

add_devilutionx_object_library(libdevilutionx_text_render
  engine/render/text_render.cpp
)
target_link_dependencies(libdevilutionx_text_render
  PUBLIC
  libdevilutionx_surface
  PRIVATE
  fmt::fmt
  unordered_dense::unordered_dense
  libdevilutionx_clx_render
  libdevilutionx_game_mode
  libdevilutionx_load_cel
  libdevilutionx_load_clx
  libdevilutionx_load_pcx
  libdevilutionx_log
  libdevilutionx_primitive_render
  libdevilutionx_ticks
  libdevilutionx_utf8
)

add_devilutionx_object_library(libdevilutionx_ticks
  engine/ticks.cpp
)
target_link_dependencies(libdevilutionx_ticks PRIVATE
  DevilutionX::SDL
)

add_devilutionx_object_library(libdevilutionx_txtdata
  data/mpq_file.cpp
  data/parser.cpp
  data/record_reader.cpp
  data/value_reader.cpp
)
target_link_dependencies(libdevilutionx_txtdata PUBLIC
  fmt::fmt
  tl
  libdevilutionx_assets
  libdevilutionx_parse_int
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_utf8
  utils/string/utf8.cpp
)
target_link_dependencies(libdevilutionx_utf8 PRIVATE
  SheenBidi::SheenBidi
)

if(NOSOUND)
  add_devilutionx_object_library(libdevilutionx_sound
    engine/audio/effects_stubs.cpp
    engine/audio/sound_stubs.cpp
  )
  target_link_dependencies(libdevilutionx_sound PUBLIC
    DevilutionX::SDL
    fmt::fmt
    magic_enum::magic_enum
    tl
    unordered_dense::unordered_dense
    libdevilutionx_options
    libdevilutionx_random
    libdevilutionx_sdl2_to_1_2_backports
  )
else()
  add_devilutionx_object_library(libdevilutionx_sound
    engine/audio/effects.cpp
    engine/audio/sound.cpp
    utils/soundsample.cpp
  )
  if(USE_SDL3)
    target_link_dependencies(libdevilutionx_sound PUBLIC
      SDL3_mixer::SDL3_mixer
    )
  else()
    target_sources(libdevilutionx_sound PRIVATE
      utils/push_aulib_decoder.cpp)
    target_link_dependencies(libdevilutionx_sound PUBLIC
      SDL_audiolib::SDL_audiolib
    )
  endif()
  target_link_dependencies(libdevilutionx_sound PUBLIC
    DevilutionX::SDL
    fmt::fmt
    magic_enum::magic_enum
    tl
    unordered_dense::unordered_dense
    libdevilutionx_options
    libdevilutionx_random
    libdevilutionx_sdl2_to_1_2_backports
  )
endif()

add_devilutionx_object_library(libdevilutionx_stores
  game/stores/stores.cpp
)
target_link_dependencies(libdevilutionx_stores PUBLIC
  DevilutionX::SDL
  fmt::fmt
  tl
  libdevilutionx_clx_render
  libdevilutionx_options
  libdevilutionx_sound
  libdevilutionx_strings
)

add_devilutionx_object_library(libdevilutionx_strings
  utils/string/str_cat.cpp
  utils/string/str_case.cpp
)
target_link_dependencies(libdevilutionx_strings PRIVATE
  fmt::fmt)

add_devilutionx_object_library(libdevilutionx_utils_console
  utils/console.cpp
)

add_devilutionx_object_library(libdevilutionx_vision
  engine/vision.cpp
)
target_link_dependencies(libdevilutionx_vision PUBLIC
  tl
)

if(USE_SDL1)
  add_devilutionx_library(libdevilutionx_sdl2_to_1_2_backports STATIC
    utils/sdl/sdl2_to_1_2_backports.cpp
  )
  target_link_dependencies(libdevilutionx_sdl2_to_1_2_backports PRIVATE
    libdevilutionx_strings
    libdevilutionx_utils_console
  )
  target_link_libraries(DevilutionX::SDL INTERFACE
    libdevilutionx_sdl2_to_1_2_backports
  )
  if(APPLE)
    enable_language(OBJC)
    target_sources(libdevilutionx_sdl2_to_1_2_backports PRIVATE
      platform/macos_sdl1/SDL_filesystem.m)
    target_link_libraries(libdevilutionx_sdl2_to_1_2_backports PRIVATE
      "-framework Foundation")
  endif()
else()
  add_library(libdevilutionx_sdl2_to_1_2_backports INTERFACE)
endif()

if(IOS)
  list(APPEND libdevilutionx_SRCS platform/ios/ios_paths.m)
endif()

if(NOT DISABLE_DEMOMODE)
  list(APPEND libdevilutionx_SRCS engine/demomode.cpp)
endif()

if(NOT NONET)
  if(NOT DISABLE_TCP)
    list(APPEND libdevilutionx_SRCS
      network/authoritative/envelope_codec.cpp
      network/transport/tcp_client.cpp
      network/transport/tcp_server.cpp)
  endif()
  if(NOT DISABLE_ZERO_TIER)
    list(APPEND libdevilutionx_SRCS
      network/transport/protocol_zt.cpp
      network/transport/zerotier_native.cpp
      network/transport/zerotier_lwip.cpp)
  endif()
endif()

if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  list(APPEND libdevilutionx_SRCS
    ${DEVILUTIONX_PROTOCOL_SRCS}
    network/authoritative/server_backed_client.cpp
    network/authoritative/server_backed_configuration.cpp
    network/authoritative/item_snapshot.cpp
    network/authoritative/player_snapshot.cpp
    network/authoritative/server_backed_world_projection.cpp
    network/authoritative/server_backed_world_presentation.cpp
    network/authoritative/server_backed_player_ui.cpp
    network/authoritative/server_backed_session.cpp
    network/authoritative/server_backed_runtime.cpp
    network/authoritative/store_command.cpp
    network/authoritative/vendor_snapshot.cpp
    network/authoritative/server_backed_vendor_state.cpp
    network/authoritative/server_backed_vendor_ui.cpp)
endif()

if(NOT USE_SDL1)
  list(APPEND libdevilutionx_SRCS
    controls/devices/game_controller.cpp
    controls/touch/event_handlers.cpp
    controls/touch/gamepad.cpp
    controls/touch/renderers.cpp)
endif()

if(SCREEN_READER_INTEGRATION)
  list(APPEND libdevilutionx_SRCS
    utils/screen_reader.cpp
  )
endif()

if(DEVILUTIONX_SCREENSHOT_FORMAT STREQUAL DEVILUTIONX_SCREENSHOT_FORMAT_PCX)
  list(APPEND libdevilutionx_SRCS
    utils/image/surface_to_pcx.cpp
  )
endif()

if(DEVILUTIONX_SCREENSHOT_FORMAT STREQUAL DEVILUTIONX_SCREENSHOT_FORMAT_PNG)
  add_devilutionx_object_library(libdevilutionx_surface_to_png
    utils/image/surface_to_png.cpp
  )
  target_link_dependencies(libdevilutionx_surface_to_png
    PUBLIC
    DevilutionX::SDL
    tl
    libdevilutionx_surface
  )
  if(USE_SDL3)
    target_link_dependencies(libdevilutionx_surface_to_png PUBLIC SDL3_image::SDL3_image)
    target_compile_definitions(libdevilutionx_surface_to_png INTERFACE USE_SDL3)
  else()
    target_link_dependencies(libdevilutionx_surface_to_png PUBLIC SDL2::SDL2_image)
  endif()
endif()

add_devilutionx_object_library(libdevilutionx ${libdevilutionx_SRCS})
target_include_directories(libdevilutionx PUBLIC ${CMAKE_CURRENT_BINARY_DIR})
if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  target_include_directories(libdevilutionx PUBLIC ${CMAKE_BINARY_DIR} ${Protobuf_INCLUDE_DIRS})
  target_compile_definitions(libdevilutionx PUBLIC DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  # Object libraries compile independently: main-library definitions do not
  # reach these gameplay/control sources containing authoritative guards.
  set(DEVILUTIONX_AUTHORITATIVE_OBJECT_TARGETS
    libdevilutionx_items
    libdevilutionx_level_objects
    libdevilutionx_monster
    libdevilutionx_multiplayer
    libdevilutionx_options
    libdevilutionx_player
    libdevilutionx_quests
    libdevilutionx_spells
    libdevilutionx_stores)
  foreach(authoritative_target libdevilutionx ${DEVILUTIONX_AUTHORITATIVE_OBJECT_TARGETS})
    target_include_directories(${authoritative_target} PRIVATE
      ${CMAKE_CURRENT_BINARY_DIR} ${CMAKE_BINARY_DIR} ${Protobuf_INCLUDE_DIRS})
    target_compile_definitions(${authoritative_target} PRIVATE DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
    target_sources(${authoritative_target} PRIVATE ${DEVILUTIONX_PROTOCOL_HDRS})
    if(WIN32 AND BUILD_TESTING)
      target_compile_definitions(${authoritative_target} PRIVATE "DVL_PROTOBUF_EXPORT=__declspec(dllexport)")
    else()
      target_compile_definitions(${authoritative_target} PRIVATE DVL_PROTOBUF_EXPORT=)
    endif()
    if(NOT authoritative_target STREQUAL "libdevilutionx")
      target_link_dependencies(${authoritative_target} PRIVATE asio)
      if(TARGET protobuf::libprotobuf)
        target_link_dependencies(${authoritative_target} PRIVATE protobuf::libprotobuf)
      else()
        target_link_dependencies(${authoritative_target} PRIVATE ${Protobuf_LIBRARIES})
      endif()
    endif()
  endforeach()
endif()
target_include_directories(libdevilutionx PUBLIC ${PROJECT_SOURCE_DIR}/3rdParty/PicoSHA2)
target_link_dependencies(libdevilutionx PUBLIC
  DevilutionX::SDL
  fmt::fmt
  libsmackerdec
  ${LUA_LIBRARIES}
  magic_enum::magic_enum
  sol2::sol2
  tl
  unordered_dense::unordered_dense
  libdevilutionx_assets
  libdevilutionx_clx_render
  libdevilutionx_codec
  libdevilutionx_config
  libdevilutionx_controller_buttons
  libdevilutionx_control_mode
  libdevilutionx_crawl
  libdevilutionx_debug_console_history
  libdevilutionx_debug_overlay_state
  libdevilutionx_direction
  libdevilutionx_dun_render
  libdevilutionx_surface
  libdevilutionx_file_util
  libdevilutionx_format_int
  libdevilutionx_game_mode
  libdevilutionx_dungeon_common
  libdevilutionx_headless_mode
  libdevilutionx_ini
  libdevilutionx_init
  libdevilutionx_items
  libdevilutionx_level_objects
  libdevilutionx_light_render
  libdevilutionx_lighting
  libdevilutionx_monster
  libdevilutionx_mpq
  libdevilutionx_multiplayer
  libdevilutionx_options
  libdevilutionx_padmapper
  libdevilutionx_palette_blending
  libdevilutionx_parse_int
  libdevilutionx_pathfinding
  libdevilutionx_pkware_encrypt
  libdevilutionx_player
  libdevilutionx_primitive_render
  libdevilutionx_quests
  libdevilutionx_quick_messages
  libdevilutionx_random
  libdevilutionx_sound
  libdevilutionx_spells
  libdevilutionx_stores
  libdevilutionx_strings
  libdevilutionx_text_render
  libdevilutionx_txtdata
  libdevilutionx_ticks
  libdevilutionx_utf8
  libdevilutionx_utils_console
)
if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  # Prefer the imported target so MSVC uses Protobuf's import library rather
  # than the DLL path returned by some vcpkg FindProtobuf configurations.
  if(TARGET protobuf::libprotobuf)
    target_link_dependencies(libdevilutionx PUBLIC protobuf::libprotobuf)
  else()
    target_link_dependencies(libdevilutionx PUBLIC ${Protobuf_LIBRARIES})
  endif()
endif()
if(NOT TARGET_PLATFORM STREQUAL "dos")
  target_link_dependencies(libdevilutionx PUBLIC Threads::Threads)
endif()
if(DEVILUTIONX_SCREENSHOT_FORMAT STREQUAL DEVILUTIONX_SCREENSHOT_FORMAT_PNG)
  target_link_dependencies(libdevilutionx PUBLIC libdevilutionx_surface_to_png)
endif()

# Use file GENERATE instead of configure_file because configure_file
# does not support generator expressions.
get_property(is_multi_config GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
add_library(libdevilutionx_config INTERFACE)
if(is_multi_config)
  set(CONFIG_PATH $<CONFIG>/config.h)
  target_include_directories(libdevilutionx_config INTERFACE ${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>)
else()
  set(CONFIG_PATH config.h)
  target_include_directories(libdevilutionx_config INTERFACE ${CMAKE_CURRENT_BINARY_DIR})
endif()
file(GENERATE OUTPUT ${CONFIG_PATH} CONTENT
"#pragma once
#define PROJECT_NAME \"${PROJECT_NAME}\"
#define PROJECT_VERSION \"${PROJECT_VERSION_WITH_SUFFIX}\"
#define PROJECT_VERSION_MAJOR ${PROJECT_VERSION_MAJOR}
#define PROJECT_VERSION_MINOR ${PROJECT_VERSION_MINOR}
#define PROJECT_VERSION_PATCH ${PROJECT_VERSION_PATCH}
")

if(NOT USE_SDL1)
  genex_for_option(DEBUG)
  target_link_libraries(libdevilutionx PRIVATE "$<${DEBUG_GENEX}:imgui::imgui>")
endif()

if(SCREEN_READER_INTEGRATION)
  if(WIN32)
    target_compile_definitions(libdevilutionx PRIVATE Tolk)
    target_link_libraries(libdevilutionx PUBLIC Tolk)
  else()
    target_include_directories(libdevilutionx PUBLIC ${Speechd_INCLUDE_DIRS})
    target_link_libraries(libdevilutionx PUBLIC speechd)
  endif()
endif()

if(USE_SDL1)
  # No need for SDL_image
elseif(USE_SDL3)
  target_link_libraries(libdevilutionx PUBLIC SDL3_image::SDL3_image)
else()
  target_link_libraries(libdevilutionx PUBLIC SDL2::SDL2_image)
endif()

if(NOT NONET)
  if(NOT DISABLE_TCP)
    target_link_libraries(libdevilutionx PUBLIC asio)
  endif()
  if(PACKET_ENCRYPTION)
    target_link_libraries(libdevilutionx PUBLIC sodium)
  endif()
endif()

if(NOT NOSOUND AND NOT USE_SDL3)
  target_link_libraries(libdevilutionx PUBLIC SDL_audiolib::SDL_audiolib)
endif()

if(NOT NONET AND NOT DISABLE_ZERO_TIER)
  target_link_libraries(libdevilutionx PUBLIC zt-static)
endif()

foreach(path ${DEVILUTIONX_PLATFORM_SUBDIRECTORIES})
  add_subdirectory(${path})
endforeach()

target_link_dependencies(libdevilutionx PUBLIC ${DEVILUTIONX_PLATFORM_LINK_LIBRARIES})

if(CMAKE_CXX_COMPILER_ID MATCHES "GNU")
  if(CMAKE_CXX_COMPILER_VERSION VERSION_LESS 9 AND NOT AMIGA)
    target_link_libraries(libdevilutionx PUBLIC stdc++fs)
  endif()
elseif(CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  # Assumes libc++ (clang) is used rather than libstdc++ (gcc).
  # This is not always true but these are ancient clang versions anyway.
  if(CMAKE_CXX_COMPILER_VERSION VERSION_LESS 7)
    target_link_libraries(libdevilutionx PUBLIC c++experimental)
  elseif(CMAKE_CXX_COMPILER_VERSION VERSION_LESS 9)
    target_link_libraries(libdevilutionx PUBLIC c++fs)
  endif()
endif()
