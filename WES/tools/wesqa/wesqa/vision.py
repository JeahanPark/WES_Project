# coding=utf-8
"""스크린샷 base64 → numpy 이미지, aircv 템플릿 매칭 헬퍼."""
import base64

import cv2
import numpy as np

from aircv.template_matching import TemplateMatching


def b64_to_image(b64):
    if isinstance(b64, (bytes, bytearray)):
        raw = base64.b64decode(b64)
    else:
        raw = base64.b64decode(b64.encode("ascii") if isinstance(b64, str) else b64)
    arr = np.frombuffer(raw, dtype=np.uint8)
    return cv2.imdecode(arr, cv2.IMREAD_COLOR)


def find_template(screen_img, template_img, threshold=0.8):
    # TemplateMatching(im_search, im_source): template=찾을 것=im_search, screen=원본=im_source
    match = TemplateMatching(template_img, screen_img, threshold=threshold)
    return match.find_best_result()


def template_on_screen(screen_img, template_path, threshold=0.8):
    tpl = cv2.imread(template_path, cv2.IMREAD_COLOR)
    if tpl is None:
        raise FileNotFoundError(template_path)
    return find_template(screen_img, tpl, threshold) is not None
