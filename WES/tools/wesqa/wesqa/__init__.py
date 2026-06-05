# coding=utf-8
from poco.pocofw import Poco
from .agent import WesPocoAgent

__all__ = ["WesPoco"]

DEFAULT_HOST = "localhost"
BASE_PORT = 5001


class WesPoco(Poco):
    """게임 내 WesPocoServer에 직접 붙는 Poco. 포트 = BASE_PORT + instance."""

    def __init__(self, instance=0, host=DEFAULT_HOST, port=None, **options):
        addr = (host, port if port is not None else BASE_PORT + instance)
        agent = WesPocoAgent(addr)
        options.setdefault("action_interval", 0.5)
        options["reevaluate_volatile_attributes"] = True
        super(WesPoco, self).__init__(agent, **options)

    def sdk_version(self):
        return self.agent.get_sdk_version()

    def invoke(self, listener, **kwargs):
        """게임 내 TestManager.<listener>(**kwargs)를 호출. 반환값 또는 None."""
        cb = self.agent.rpc.call("Invoke", listener=listener, data=kwargs)
        value, error = cb.wait()
        if error is not None:
            raise RuntimeError("invoke '%s' failed: %s" % (listener, error))
        return value

    def screenshot(self, path=None, width=0):
        """현재 화면 캡처 → BGR numpy 이미지. path 주면 파일로도 저장."""
        from . import vision
        b64, _fmt = self.agent.screen.getScreen(width)
        img = vision.b64_to_image(b64)
        if path:
            import cv2
            cv2.imwrite(path, img)
        return img
