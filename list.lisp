;; requires prelude.lisp

(setq null? ((curry equal) '()))

(func list! (on-nil on-cons)
  (lambda (lst)
    (cond (null? lst)
      on-nil
      (on-cons (head lst) (tail lst)))))

(func length (lst)
  ((list!
    0
    (lambda (_ xs) (plus 1 (length xs)))
  ) lst))

(func map (f lst)
  (cond (null? lst)
    '()
    (cons (f (head lst)) (map f (tail lst)))))

(func filter (p lst)
  ((list!
    '()
    (lambda (x xs)
      (cond (p x)
        (cons x (filter p xs))
                (filter p xs))))
   lst))


(func foldl (f acc lst)
  (prog ()
    (while (not (null? lst)) (prog ()
      (setq acc (f acc (head lst)))
      (setq lst (tail lst))))
    acc))

(func sum (lst) (foldl plus 0 lst))

(func append (l1 l2)
  ((list!
     l2
     (lambda (h t) (cons h (append t l2)))) l1))

;; (func append (l1 l2)
;;   (cond (null? l1)
;;     l2
;;     (cons (head l1) (append (tail l1) l2))))

(func flat (lst)
  (foldl append '() lst))

(func flat-map (f)
  (compose flat ((curry map) f)))

(func reverse (lst)
  ((list!
    '()
    (lambda (x xs) (append (reverse xs) (singleton x)))
  ) lst))

(func singleton (x) (cons x '()))
