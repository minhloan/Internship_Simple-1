model ws_pos_bridge

global {
  bool paused <- false;
  init { create net number: 1; }
}

species marker {
  string id;
  aspect default { draw circle(0.5) color: rgb(255,0,0); }
}

species net skills: [network] {

  init {
    do connect protocol: "websocket_client"
      to: "127.0.0.1"
      port: 3001
      with_name: "ws"
      raw: true;
    do send to: "ws" contents: "{\"cmd\":\"hello_from_gama\"}";
    write "GAMA: ws connected; hello sent.";
  }

  // nhận từ Unity: pause/resume/pos và log giống ảnh bạn đưa
  reflex rx {
    do fetch_message_from_network;

    loop i from: 1 to: 64 {
      if (!has_more_message()) { i <- 64; } else {
        message m <- fetch_message();
        if (m = nil or m.contents = nil) { i <- 64; } else {
          string s <- string(m.contents);
          write "rx: " + s;

          map js <- from_json(s);
          if (js != nil and contains_key(js, "cmd")) {
            string cmd <- string(js["cmd"]);

            if (cmd = "pause")  { paused <- true;  }
            if (cmd = "resume") { paused <- false; }

            if (cmd = "pos") {
              string pid <- string(js["id"]);
              float  px  <- float(js["x"]);
              float  py  <- float(js["y"]);

              if (empty(marker where (each.id = pid))) {
                create marker number: 1 with: [id:: pid, location:: {px, py}];
              } else {
                ask first(marker where (each.id = pid)) { location <- {px, py}; }
              }

              // phản hồi xác nhận một chiều GAMA->Unity (tùy chọn)
              do send to: "ws" contents: "{\"ack\":\"ok\",\"id\":\"" + pid + "\"}";
            }
          }
        }
      }
    }
  }
}

experiment e type: gui {
  output { display d1 { species marker; } }
}
