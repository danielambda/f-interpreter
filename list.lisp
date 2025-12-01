(func list! (on-nil on-cons)
  (lambda (lst)
    (cond (null? lst)
      on-nil
      (on-cons (head lst) (tail lst)))))

(func curry (f)
  (lambda (x) (lambda (y) (f x y))))

(func uncurry (f)
  (lambda (x y) ((f x) y)))

(func compose (f g)
  (lambda (x) (f (g x))))

(func id (x) x)

(func const (x)
  (lambda (_) x))

;; (setq null? ((curry equal) '()))
(func null? (x) (equal '() x))

(func map (f lst)
  (cond (null? lst)
    '()
    (cons (f (head lst)) (map f (tail lst)))))

(map ((curry plus) 5) '(1 2 3 4))


(setq filter (uncurry (lambda (p)
  (prog ()
    (func go (lst)
      (list! '() (lambda (h t)
        (cond (p h)
          (cons h (go t))
          (go t)))))
    go))))

(setq orr (lambda (p1 p2)
  (lambda (x) (or (p1 x) (p2 x)))))
(func andd (p1 p2)
  (lambda (x) (andd (p1 x) (p2 x))))

(filter (orr isint isbool) '(a b c true 5 2 false false true 2))

'hello-world

(func foldl (f acc lst)
  (prog ()
    (while (not (null? lst)) (prog ()
      (setq acc (f acc (head lst)))
      (setq lst (tail lst))))
    acc))

(func sum (lst) (foldl plus 0 lst))

(sum '(1 2.3 3 4))


(func append (l1 l2)
  ((list!
     l2
     (lambda (h t) (cons h (append t l2)))) l1))

(func append_ (l1 l2)
  (cond (null? l1)
    l2
    (cons (head l1) (append (tail l1) l2))))

(func flat (lst)
  (foldl append '() lst))

(func flatMap (f)
  (compose flat ((curry map) f)))

((flatMap (lambda (x) (cons x '(x y)))) '(1 2 3 4))
